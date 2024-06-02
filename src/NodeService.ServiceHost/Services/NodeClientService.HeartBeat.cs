using NodeService.ServiceHost.Helpers;
using System.Management;

namespace NodeService.ServiceHost.Services
{
    public partial class NodeClientService
    {


        private async Task ProcessHeartBeatRequest(NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            HeartBeatResponse heartBeatRsp = new HeartBeatResponse
            {
                RequestId = subscribeEvent.HeartBeatRequest.RequestId
            };

            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                heartBeatRsp.Properties.Add(NodePropertyModel.HostName_Key, Dns.GetHostName());
                heartBeatRsp.Properties.Add(NodePropertyModel.ClientVersion_Key, Constants.Version);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_UserName_Key, Environment.UserName);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_ProcessorCount_Key, Environment.ProcessorCount.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_IsPrivilegedProcess_Key, Environment.IsPrivilegedProcess.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_UserInteractive_Key, Environment.UserInteractive.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_SystemDirectory_Key, Environment.SystemDirectory);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_OSVersion_Key, Environment.OSVersion.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_UserDomainName_Key, Environment.UserDomainName);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_CommandLine_Key, Environment.CommandLine);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_WorkingSet_Key, Environment.WorkingSet.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_SystemPageSize_Key, Environment.SystemPageSize.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_ProcessPath_Key, Environment.ProcessPath);
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_Version_Key, Environment.Version.ToString());
                heartBeatRsp.Properties.Add(NodePropertyModel.Environment_LogicalDrives_Key, string.Join(",", Environment.GetLogicalDrives()));
                heartBeatRsp.Properties.Add(NodePropertyModel.NetworkInterface_IsNetworkAvailable_Key, NetworkInterface.GetIsNetworkAvailable().ToString());


                CollectDomain(heartBeatRsp);

                CollectEnvironmentVariables(heartBeatRsp);

                CollectNetworkInterfaces(heartBeatRsp);

                CollectProcessList(heartBeatRsp);

                CollectDiskInfo(heartBeatRsp);

                CollectWin32Services(heartBeatRsp);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            finally
            {
                heartBeatRsp.Properties.Add(NodePropertyModel.LastUpdateDateTime_Key, DateTime.Now.ToString(NodePropertyModel.DateTimeFormatString));

            }


            stopwatch.Stop();

            heartBeatRsp.Properties.Add("CollectTimeSpan", stopwatch.Elapsed.ToString());
            await client.SendHeartBeatResponseAsync(heartBeatRsp, _headers, null, cancellationToken);
            IncreaseHeartBeatCounter();

            void CollectNetworkInterfaces(HeartBeatResponse heartBeatRsp)
            {
                try
                {
                    if (!heartBeatRsp.Properties.ContainsKey(NodePropertyModel.FactoryName_key))
                    {
                        heartBeatRsp.Properties.TryAdd(NodePropertyModel.FactoryName_key, "Unknown");
                    }
                    var networkInterfaceModels = NetworkInterface.GetAllNetworkInterfaces().Select(NetworkInterfaceModel.From);
                    heartBeatRsp.Properties.Add(NodePropertyModel.NetworkInterface_AllNetworkInterfaces_Key, JsonSerializer.Serialize(networkInterfaceModels));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }

            void CollectProcessList(HeartBeatResponse heartBeatRsp)
            {
                var processList = CommonHelper.CollectProcessList(_logger);

                heartBeatRsp.Properties.Add(NodePropertyModel.Process_Processes_Key, JsonSerializer.Serialize(processList));
            }

            void CollectEnvironmentVariables(HeartBeatResponse heartBeatRsp)
            {

                List<EnvironmentVariableInfo> environmentVariables = new List<EnvironmentVariableInfo>();
                try
                {
                    foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Machine))
                    {

                        environmentVariables.Add(new EnvironmentVariableInfo()
                        {
                            Name = entry.Key as string,
                            Value = entry.Value as string
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

                heartBeatRsp.Properties.Add(
                    NodePropertyModel.Environment_EnvironmentVariables_Key,
                    JsonSerializer.Serialize(environmentVariables));
            }

            void CollectDomain(HeartBeatResponse heartBeatRsp)
            {
                try
                {
                    Domain domain = Domain.GetComputerDomain();
                    heartBeatRsp.Properties.Add(NodePropertyModel.Domain_ComputerDomain_Key, domain.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }

            void CollectDiskInfo(HeartBeatResponse heartBeatRsp)
            {
                try
                {


                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }

            void CollectWin32Services(HeartBeatResponse heartBeatRsp)
            {
                try
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        return;
                    }
                    using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Service");
                    using var objectCollection = searcher.Get();
                    List<ServiceProcessInfo> services = new List<ServiceProcessInfo>(objectCollection.Count);
                    foreach (var service in objectCollection)
                    {
                        ServiceProcessInfo serviceProcessInfo = new ServiceProcessInfo();

                        foreach (var property in service.Properties)
                        {
                            try
                            {
                                switch (property.Name)
                                {
                                    case nameof(ServiceProcessInfo.Name):
                                        serviceProcessInfo.Name = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.PathName):
                                        serviceProcessInfo.PathName = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.ProcessId):
                                        serviceProcessInfo.ProcessId = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.Started):
                                        serviceProcessInfo.Started = (bool)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.StartMode):
                                        serviceProcessInfo.StartMode = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.ExitCode):
                                        serviceProcessInfo.ExitCode = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.Status):
                                        serviceProcessInfo.Status = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.SystemName):
                                        serviceProcessInfo.SystemName = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.WaitHint):
                                        serviceProcessInfo.WaitHint = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.Caption):
                                        serviceProcessInfo.Caption = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.CheckPoint):
                                        serviceProcessInfo.CheckPoint = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.InstallDate):
                                        serviceProcessInfo.InstallDate = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.AcceptPause):
                                        serviceProcessInfo.AcceptPause = (bool)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.AcceptStop):
                                        serviceProcessInfo.AcceptStop = (bool)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.CreationClassName):
                                        serviceProcessInfo.CreationClassName = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.DelayedAutoStart):
                                        serviceProcessInfo.DelayedAutoStart = (bool)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.Description):
                                        serviceProcessInfo.Description = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.DesktopInteract):
                                        serviceProcessInfo.DesktopInteract = (bool)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.ErrorControl):
                                        serviceProcessInfo.ErrorControl = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.ServiceSpecificExitCode):
                                        serviceProcessInfo.ServiceSpecificExitCode = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.ServiceType):
                                        serviceProcessInfo.ServiceType = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.StartName):
                                        serviceProcessInfo.StartName = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.SystemCreationClassName):
                                        serviceProcessInfo.SystemCreationClassName = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.TagId):
                                        serviceProcessInfo.TagId = (uint)property.Value;
                                        break;
                                    case nameof(ServiceProcessInfo.State):
                                        serviceProcessInfo.State = property.Value as string;
                                        break;
                                    case nameof(ServiceProcessInfo.DisplayName):
                                        serviceProcessInfo.DisplayName = property.Value as string;
                                        break;

                                    default:
                                        break;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine(ex.ToString());
                            }
                        }

                        services.Add(serviceProcessInfo);

                    }
                    heartBeatRsp.Properties.Add(NodePropertyModel.System_Win32Services_Key, JsonSerializer.Serialize(services));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
        }

        private void IncreaseHeartBeatCounter()
        {
            Interlocked.Increment(ref _heartBeatCounter);
        }

        private long GetHeartBeatCounter()
        {
            return Interlocked.Read(ref _heartBeatCounter);
        }
    }
}
