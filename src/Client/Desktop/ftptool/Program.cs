using CommandLine;
using System.Diagnostics;
using System.Text.Json;

namespace ftptool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            Parser.Default.ParseArguments<Options>(args)
             .WithParsed((options) =>
             {
                 if (Debugger.IsAttached)
                 {
                     options.rsp = "D:\\repos\\JobsWorkerDaemonService\\ftptool\\config\\ret.bat";
                 }
                 ProcessArgs(options, args);
             });
        }

        private static void ProcessArgs(Options options, string[] args)
        {
            try
            {
                if (options.rsp == null)
                {
                    return;
                }
                if (!File.Exists(options.rsp))
                {
                    Console.WriteLine($"Could not found rsp file {options.rsp}");
                    return;
                }
                Response response = null;
                using (var fileStream = File.OpenRead(options.rsp))
                {
                    response = JsonSerializer.Deserialize<Response>(fileStream);
                }
                FtpTaskExecutor ftpTaskExecutor = new FtpTaskExecutor(response);
                ftpTaskExecutor.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }


        }


    }
}
