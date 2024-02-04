using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorker.Shared.Models
{
    public class NodeProperties
    {
        public const string NodeName_Key = "NodeName";
        public const string LastUpdateDateTime_Key = "LastUpdateDateTime";
        public const string Version_Key = "Version";
        public const string Environment_UserName_Key = "Environment.UserName";
        public const string Environment_ProcessorCount_Key = "Environment.ProcessorCount";
        public const string Environment_IsPrivilegedProcess_Key = "Environment.IsPrivilegedProcess";
        public const string Environment_UserInteractive_Key = "Environment.UserInteractive";
        public const string Environment_SystemDirectory_Key = "Environment.SystemDirectory";
        public const string Environment_LogicalDrives_Key = "Environment.LogicalDrives";
        public const string Environment_OSVersion_Key = "Environment.OSVersion";
        public const string Environment_UserDomainName_Key = "Environment.UserDomainName";
        public const string Environment_CommandLine_Key = "Environment.CommandLine";
        public const string Environment_WorkingSet_Key = "Environment.WorkingSet";
        public const string Environment_SystemPageSize_Key = "Environment.SystemPageSize";
        public const string Environment_ProcessPath_Key = "Environment.ProcessPath";
        public const string Environment_Version_Key = "Environment.Version";
        public const string Environment_EnvironmentVariables_Key = "Environment.EnvironmentVariables";
        public const string NetworkInterface_IsNetworkAvailable_Key = "NetworkInterface.IsNetworkAvailable";
        public const string NetworkInterface_AllNetworkInterfaces_Key = "NetworkInterface.AllNetworkInterfaces";
        public const string Process_Processes_Key = "Process.Processes";
        public const string Collect_TimeSpan_Key = "Collect.TimeSpan";
        public const string Domain_ComputerDomain_Key = "Domain.ComputerDomain";
        public const string CollectTimeSpan_Key = "CollectTimeSpan";


        public string NodeName { get; set; }

        public string Version { get; set; }

        public string LastUpdateDateTime { get; set; }

        public int ProcessorCount { get; set; }

        public string UserName { get; set; }

        public bool IsPrivilegedProcess { get; set; }

        public bool UserInteractive { get; set; }

        public string SystemDirectory { get; set; }

        public string OSVersion {  get; set; }

        public string ProcessPath { get; set; }
        public long SystemPageSize { get; private set; }
        public string UserDomainName { get; private set; }

        public string RuntimeVersion { get; private set; }

        public long WorkingSet { get; private set; }

        public string LogicalDrives { get; set; }

        public bool IsNetworkAvailable { get; set; }

        public string ComputerDomain { get; set; }

        public IEnumerable<NetworkInterfaceModel> NetworkInterfaces { get; set; }

        public IEnumerable<ProcessInfo> Processes { get; set; }

        public IEnumerable<EnvironmentVariableInfo> EnvironmentVariables { get; set; }
        public string CollectTimeSpan { get; set; }

        public static NodeProperties FromNodePropertyItems(NodePropertyItem[] items)
        {
            var properties = new NodeProperties();

            if (items != null)
            {
                foreach (var nodePropertyItem in items)
                {
                    switch (nodePropertyItem.Name)
                    {
                        case NodeName_Key:
                            properties.NodeName = nodePropertyItem.Value;
                            break;
                        case LastUpdateDateTime_Key:
                            properties.LastUpdateDateTime = nodePropertyItem.Value;
                            break;
                        case Version_Key:
                            properties.Version = nodePropertyItem.Value;
                            break;
                        case Environment_UserName_Key:
                            properties.UserName = nodePropertyItem.Value;
                            break;
                        case Environment_ProcessorCount_Key:
                            properties.ProcessorCount = int.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_UserInteractive_Key:
                            properties.UserInteractive = bool.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_IsPrivilegedProcess_Key:
                            properties.IsPrivilegedProcess = bool.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_LogicalDrives_Key:
                            properties.LogicalDrives = nodePropertyItem.Value;
                            break;
                        case Environment_SystemDirectory_Key:
                            properties.SystemDirectory = nodePropertyItem.Value;
                            break;
                        case Environment_OSVersion_Key:
                            properties.OSVersion = nodePropertyItem.Value;
                            break;
                        case Environment_EnvironmentVariables_Key:
                            properties.EnvironmentVariables = JsonSerializer.Deserialize<EnvironmentVariableInfo[]>(nodePropertyItem.Value);
                            break;
                        case Environment_ProcessPath_Key:
                            properties.ProcessPath = nodePropertyItem.Value;
                            break;
                        case Environment_SystemPageSize_Key:
                            properties.SystemPageSize = long.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_UserDomainName_Key:
                            properties.UserDomainName = nodePropertyItem.Value;
                            break;
                        case Environment_WorkingSet_Key:
                            properties.WorkingSet = long.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_Version_Key:
                            properties.Version = nodePropertyItem.Value;
                            break;
                        case NetworkInterface_IsNetworkAvailable_Key:
                            properties.IsNetworkAvailable = bool.Parse(nodePropertyItem.Value);
                            break;
                        case NetworkInterface_AllNetworkInterfaces_Key:
                            properties.NetworkInterfaces = JsonSerializer.Deserialize<NetworkInterfaceModel[]>(nodePropertyItem.Value);
                            break;
                        case Process_Processes_Key:
                            properties.Processes = JsonSerializer.Deserialize<ProcessInfo[]>(nodePropertyItem.Value);
                            break;
                        case Domain_ComputerDomain_Key:
                            properties.ComputerDomain = nodePropertyItem.Value;
                            break;
                        case CollectTimeSpan_Key:
                            properties.CollectTimeSpan = nodePropertyItem.Value;
                            break;
                        default:
                            break;
                    }
                }
            }
            return properties;
        }



    }
}
