using NodeService.WindowsService;
using NodeService.WindowsService.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.WindowsService.Services
{
    public partial class NodeClientService
    {
        private long _heartBeatCounter;

        private async Task ProcessHeartBeatRequest(NodeServiceClient client, SubscribeEvent subscribeEvent, CancellationToken cancellationToken = default)
        {
            HeartBeatResponse heartBeatRsp = new HeartBeatResponse();
            heartBeatRsp.RequestId = subscribeEvent.HeartBeatRequest.RequestId;

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

                heartBeatRsp.Properties.Add(NodePropertyModel.LastUpdateDateTime_Key, DateTime.Now.ToString(NodePropertyModel.DateTimeFormatString));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }


            stopwatch.Stop();

            heartBeatRsp.Properties.Add("CollectTimeSpan", stopwatch.Elapsed.ToString());
            await client.SendHeartBeatResponseAsync(heartBeatRsp, _headers, null, cancellationToken);

            void CollectNetworkInterfaces(HeartBeatResponse heartBeatRsp)
            {
                try
                {
                    string name = Dns.GetHostName();
                    IPAddress[] ipAddressList = Dns.GetHostAddresses(name);
                    foreach (IPAddress ipa in ipAddressList)
                    {
                        if (ipa.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ipAddressString = ipa.ToString();
                            if (ipAddressString.StartsWith("10."))
                            {
                                heartBeatRsp.Properties.Add(NodePropertyModel.FactoryName_key, "BL");
                                break;
                            }
                            else if (ipAddressString.StartsWith("172."))
                            {
                                heartBeatRsp.Properties.Add(NodePropertyModel.FactoryName_key, "GM");
                                break;
                            }
                        }
                    }
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
                //_heartBeatCounter++;
                //if (_heartBeatCounter % 40 != 0)
                //{
                //    return;
                //}

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

                    //heartBeatRsp.Properties.Add(NodePropertyModel.Domain_ComputerDomain_Key, domain.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }
        }
    }
}
