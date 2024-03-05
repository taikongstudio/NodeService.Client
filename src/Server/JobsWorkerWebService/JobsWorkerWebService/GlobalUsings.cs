
global using AntDesign.Charts;
global using AntDesign.ProLayout;
global using CommandLine;
global using FluentFTP;
global using Google.Protobuf.WellKnownTypes;
global using Grpc.Core;
global using Grpc.Net.Client;
global using JobsWorker.Shared;
global using JobsWorker.Shared.Models;
global using JobsWorkerWebService;
global using JobsWorkerWebService.BackgroundServices;
global using JobsWorkerWebService.BackgroundServices.Models;
global using JobsWorkerWebService.Data;
global using JobsWorkerWebService.Extensions;
global using JobsWorkerWebService.GrpcServices;
global using JobsWorkerWebService.GrpcServices.Models;
global using JobsWorkerWebService.Models.Configurations;
global using JobsWorkerWebService.Services;
global using JobsWorkerWebService.Services.VirtualSystem;
global using Microsoft.AspNetCore.Components;
global using Microsoft.AspNetCore.Http.Features;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Caching.Memory;
global using NLog.Extensions.Logging;
global using Quartz;
global using Quartz.Impl;
global using System.Buffers.Text;
global using System.Collections.Concurrent;
global using System.Diagnostics;
global using System.Runtime.CompilerServices;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using System.Threading.Tasks.Dataflow;
global using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
global using GlobalNodeTaskDictionary =
    System.Collections.Concurrent.ConcurrentDictionary<
        string,
        System.Collections.Concurrent.ConcurrentDictionary<string, JobsWorkerWebService.Services.JobScheduleTask>>;

