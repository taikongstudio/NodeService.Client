
global using CommandLine;
global using NodeService.WindowsService.Services;
global using NLog;
global using NLog.Web;
global using NodeService.Infrastructure.MessageQueues;
global using NodeService.Infrastructure.Models;
global using Quartz;
global using Quartz.Impl;
global using System.Text.Json;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Google.Protobuf.WellKnownTypes;
global using Grpc.Core;
global using Grpc.Net.Client;
global using Microsoft.Extensions.Configuration;
global using NodeService.WindowsService;
global using NodeService.WindowsService.Services;
global using NodeService.Infrastructure;
global using NodeService.Infrastructure.DataModels;
global using NodeService.Infrastructure.MessageQueues;
global using NodeService.Infrastructure.Models;
global using Quartz;
global using System.Collections;
global using System.Diagnostics;
global using System.DirectoryServices.ActiveDirectory;
global using System.Net;
global using System.Net.Http.Json;
global using System.Net.NetworkInformation;
global using System.Net.Sockets;
global using System.Text.Json;
global using System.Threading.Tasks.Dataflow;

global using static NodeService.Infrastructure.Services.NodeService;

global using JobExecutionStatus = NodeService.Infrastructure.Models.JobExecutionReport.Types.JobExecutionStatus;

global using ILogger = Microsoft.Extensions.Logging.ILogger;
