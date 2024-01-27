using JobsWorkerDaemonService.Workers;
using NLog;
using NLog.Web;
using Quartz;
using Quartz.Impl;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace JobsWorkerDaemonService
{
    public class Program
	{
		public static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            Logger logger = NLogBuilder.ConfigureNLog("Nlog.config").GetCurrentClassLogger();
			logger.Log(NLog.LogLevel.Info, $"args:{Environment.CommandLine}");
			LogManager.AutoShutdown = true;
            try
			{
                if (args.Any(x => x == "--check"))
                {
                    TryStartService(logger);
                    return;
                }

                if (!CheckSingleton(logger))
                {
                    return;
                }

                SetupTaskSchedule(logger);
                SetupStartup(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "JobsWorkerDaemonService", logger);

                IHost host = Host.CreateDefaultBuilder(args)
					.UseWindowsService()
					.ConfigureServices(services =>
					{
						services.AddHostedService<ServiceWorker>();
						services.AddHostedService<JobWorker>();
						services.AddHostedService<UploadLogsJobWorker>();
						services.AddHostedService<UpdateServerConfigWorker>();
                        services.AddSingleton<ISchedulerFactory>(new StdSchedulerFactory());
					})
					.ConfigureLogging(loggingBuilder =>
					{
						loggingBuilder.ClearProviders();
						loggingBuilder.AddConsole();
					})
					.UseNLog()
					.Build();


				host.Run();
			}
			catch (Exception ex)
			{
				logger.Fatal(ex);
			}
			finally
			{
				LogManager.Shutdown();
			}

		}

		private static void SetupStartup(string directory, string shortcutName, NLog.Logger logger)
		{
			try
			{
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				//添加引用 Com 中搜索 Windows Script Host Object Model
				string shortcutPath = Path.Combine(directory, string.Format("{0}.lnk", shortcutName));

				if (File.Exists(shortcutPath))
				{
					File.Delete(shortcutPath);
					return;
				}
			}
			catch (Exception ex)
			{
				logger.Error(ex.ToString());
			}

		}

		private static bool CheckSingleton(Logger logger)
		{
			string guid = "Global\\AECA7B61-AE5B-443B-849F-D6F4B95F28C5";
			Mutex mutex = null;

            try
			{
                if (Mutex.TryOpenExisting(guid, out mutex))
                {
					logger.Fatal("CheckSingleton:exit");
                    return false;
                }
            }
			catch(UnauthorizedAccessException e)
			{
                logger.Fatal("CheckSingleton:exit");
                return false;
            }
			catch (Exception ex)
			{

			}

			mutex = new Mutex(true, guid);
			return true;
		}

		private static void RunProcess(Logger? logger, string cmdLine)
		{
			using (Process process = new Process())
			{
				try
				{
					process.StartInfo.FileName = "C:\\Windows\\System32\\cmd.exe";
					process.StartInfo.Arguments = "/c " + cmdLine;
					process.StartInfo.CreateNoWindow = true;
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.RedirectStandardError = true;

                    process.Start();

					var outputDataRecieveEventHandler = new DataReceivedEventHandler((s, de) =>
					{
						logger?.Log(NLog.LogLevel.Info, de.Data);
					});
					process.OutputDataReceived += outputDataRecieveEventHandler;
					process.ErrorDataReceived += outputDataRecieveEventHandler;
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();


					// 等待脚本执行完毕
					process.WaitForExit();
					process.OutputDataReceived -= outputDataRecieveEventHandler;
					process.ErrorDataReceived -= outputDataRecieveEventHandler;
				}
				catch (Exception ex)
				{
					logger.Error(ex.ToString());
				}


			}
		}

		private static void SetupTaskSchedule(Logger logger)
		{
			try
			{
				Task.Run(() =>
				{
					logger.Log(NLog.LogLevel.Info, "SetupTaskSchedule");
					RunProcess(logger, "C:\\Windows\\System32\\schtasks.exe /DELETE /tn RunDaemonTaskSH /F");
					string cmdArgs = $"C:\\Windows\\System32\\schtasks.exe /create /RU SYSTEM /RL HIGHEST /sc minute /mo 10 /tn RunDaemonTaskSH /tr \"'{Process.GetCurrentProcess().MainModule.FileName}' --check\" /st 00:00";
					logger.Log(NLog.LogLevel.Info, cmdArgs);
					RunProcess(logger, cmdArgs);
					logger.Log(NLog.LogLevel.Info, "Create Task success");
				});
			}
			catch (Exception ex)
			{
				logger.Fatal(ex.ToString());
			}

		}

		private static void TryStartService(Logger logger)
		{
			try
			{
				logger.Info("try start Service");
				RunProcess(logger, "C:\\Windows\\System32\\sc.exe start JobsWorkerDaemonServiceWindowsService");
            }
			catch (Exception ex)
			{
				logger.Error(ex);
            }
		}
	}
}