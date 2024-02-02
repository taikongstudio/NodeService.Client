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
        public const string NodeNameKey = "NodeName";
        public const string LastUpdateDateTimeKey = "LastUpdateDateTime";
        public const string VersionKey = "Version";
        public const string Environment_UserNameKey = "Environment.UserName";
        public const string Environment_ProcessorCountKey = "Environment.ProcessorCount";
        public const string Environment_IsPrivilegedProcessKey = "Environment.IsPrivilegedProcess";
        public const string Environment_UserInteractiveKey = "Environment.UserInteractive";
        public const string Environment_SystemDirectoryKey = "Environment.SystemDirectory";
        public const string Environment_LogicalDrivesKey = "Environment.LogicalDrives";
        public const string Environment_OSVersionKey = "Environment.OSVersion";
        public const string Environment_UserDomainNameKey = "Environment.UserDomainName";
        public const string Environment_CommandLineKey = "Environment.CommandLine";
        public const string Environment_WorkingSetKey = "Environment.WorkingSet";
        public const string Environment_SystemPageSizeKey = "Environment.SystemPageSize";
        public const string Environment_ProcessPathKey = "Environment.ProcessPath";
        public const string Environment_VersionKey = "Environment.Version";
        public const string Environment_EnvironmentVariablesKey = "Environment.EnvironmentVariables";
        public const string NetworkInterface_IsNetworkAvailableKey = "NetworkInterface.IsNetworkAvailable";
        public const string NetworkInterface_AllNetworkInterfacesKey = "NetworkInterface.AllNetworkInterfaces";
        public const string Process_ProcessesKey = "Process.Processes";
        public const string Collect_TimeSpanKey = "Collect.TimeSpan";


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

        public IEnumerable<NetworkInterfaceModel> NetworkInterfaces { get; set; }

        public IEnumerable<ProcessInfo> Processes { get; set; }

        public IEnumerable<EnvironmentVariableInfo> EnvironmentVariables { get; set; }


        public static NodeProperties FromNodePropertyItems(NodePropertyItem[] items)
        {
            var properties = new NodeProperties();

            if (items != null)
            {
                foreach (var nodePropertyItem in items)
                {
                    switch (nodePropertyItem.Name)
                    {
                        case NodeNameKey:
                            properties.NodeName = nodePropertyItem.Value;
                            break;
                        case LastUpdateDateTimeKey:
                            properties.LastUpdateDateTime = nodePropertyItem.Value;
                            break;
                        case VersionKey:
                            properties.Version = nodePropertyItem.Value;
                            break;
                        case Environment_UserNameKey:
                            properties.UserName = nodePropertyItem.Value;
                            break;
                        case Environment_ProcessorCountKey:
                            properties.ProcessorCount = int.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_UserInteractiveKey:
                            properties.UserInteractive = bool.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_IsPrivilegedProcessKey:
                            properties.IsPrivilegedProcess = bool.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_LogicalDrivesKey:
                            properties.LogicalDrives = nodePropertyItem.Value;
                            break;
                        case Environment_SystemDirectoryKey:
                            properties.SystemDirectory = nodePropertyItem.Value;
                            break;
                        case Environment_OSVersionKey:
                            properties.OSVersion = nodePropertyItem.Value;
                            break;
                        case Environment_EnvironmentVariablesKey:
                            properties.EnvironmentVariables = JsonSerializer.Deserialize<EnvironmentVariableInfo[]>(nodePropertyItem.Value);
                            break;
                        case Environment_ProcessPathKey:
                            properties.ProcessPath = nodePropertyItem.Value;
                            break;
                        case Environment_SystemPageSizeKey:
                            properties.SystemPageSize = long.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_UserDomainNameKey:
                            properties.UserDomainName = nodePropertyItem.Value;
                            break;
                        case Environment_WorkingSetKey:
                            properties.WorkingSet = long.Parse(nodePropertyItem.Value);
                            break;
                        case Environment_VersionKey:
                            properties.Version = nodePropertyItem.Value;
                            break;
                        case NetworkInterface_IsNetworkAvailableKey:
                            properties.IsNetworkAvailable = bool.Parse(nodePropertyItem.Value);
                            break;
                        case NetworkInterface_AllNetworkInterfacesKey:
                            properties.NetworkInterfaces = JsonSerializer.Deserialize<NetworkInterfaceModel[]>(nodePropertyItem.Value);
                            break;
                        case Process_ProcessesKey:
                            properties.Processes = JsonSerializer.Deserialize<ProcessInfo[]>(nodePropertyItem.Value);
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
