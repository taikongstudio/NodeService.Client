﻿using NodeService.ServiceHost.Models;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Services
{
    public class ProcessServerService : BackgroundService
    {
        private readonly ILogger<ProcessServerService> _logger;
        private readonly ServiceOptions _serviceOptions;

        public ProcessServerService(
            ILogger<ProcessServerService> logger,
            ServiceOptions serviceOptions)
        {
            _logger = logger;
            _serviceOptions = serviceOptions;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    string pipeName = $"NodeService.ServiceHost-{Environment.ProcessId}";
                    using var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut);

                    _logger.LogInformation($"NamedPipeServerStream {pipeName} created.");



                    // Wait for a client to connect
                    _logger.LogInformation("Waiting for client connection...");

                    await pipeServer.WaitForConnectionAsync(stoppingToken);

                    _logger.LogInformation("Client connected.");

                    while (!stoppingToken.IsCancellationRequested)
                    {
                        try
                        {
                            var req = await ReadCommandRequest(pipeServer, stoppingToken);

                            switch (req.CommadType)
                            {
                                case ProcessCommandType.KillProcess:
                                    {
                                        var rsp = new ProcessCommandResponse();
                                        await WriteCommandResponse(pipeServer, rsp, stoppingToken);
                                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                                    }
                                    Environment.Exit(0);
                                    break;
                                case ProcessCommandType.HeartBeat:
                                    {
                                        var rsp = new ProcessCommandResponse();
                                        await WriteCommandResponse(pipeServer, rsp, stoppingToken);
                                    }
                                    break;
                                default:
                                    break;
                            }

                        }
                        // Catch the IOException that is raised if the pipe is broken
                        // or disconnected.
                        catch (IOException ex)
                        {
                            _logger.LogError(ex.ToString());
                        }
                        finally
                        {

                        }
                    }


                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

        }

        private async Task WriteCommandResponse(Stream  stream,
    ProcessCommandResponse rsp,
    CancellationToken cancellationToken = default)
        {
            using var streamWriter = new StreamWriter(stream, leaveOpen: true);
            streamWriter.AutoFlush = true;
            var jsonString = JsonSerializer.Serialize(rsp);
            _logger.LogInformation($"Server send rsp:{jsonString}.");
            await streamWriter.WriteAsync(jsonString);
            await streamWriter.WriteLineAsync();
        }

        private async Task<ProcessCommandRequest> ReadCommandRequest(
            Stream  stream,
            CancellationToken cancellationToken = default)
        {
            using var streamReader = new StreamReader(stream, leaveOpen: true);
            var jsonString = await streamReader.ReadLineAsync(cancellationToken);
            _logger.LogInformation($"Server recieve req:{jsonString}.");
            var rsp = JsonSerializer.Deserialize<ProcessCommandRequest>(jsonString);
            return rsp;
        }
    }
}
