using NModbus;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NModbus.Serial;
using System.Net;
using System.Net.Sockets;
using NodeService.DeviceHost.Data.Models;
using Microsoft.EntityFrameworkCore;
using NodeService.DeviceHost.Data;
using Microsoft.EntityFrameworkCore.Internal;

namespace NodeService.DeviceHost.Devices
{
    public class YinDuDeviceOptions
    {
        public string Host { get; set; }

        public int Port { get; set; }
    }

    public class YinDuKeJiDevice : Device
    {
        readonly ILogger<YinDuKeJiDevice> _logger;
        readonly YinDuDeviceOptions _options;
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        int _optionChangesCount;
        int _connectionErrorCount;
        TcpClient _client;
        IModbusMaster _master;
        DateTime _lastSamplingDateTime;

        public YinDuKeJiDevice(
            ILogger<YinDuKeJiDevice> logger,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            YinDuDeviceOptions options)
        {
            _logger = logger;
            _options = options;
            _dbContextFactory = dbContextFactory;
        }

        public override async ValueTask<bool> ConnectAsync()
        {
            try
            {
                if (_connectionErrorCount > 0 || _optionChangesCount > 0)
                {
                    Reset();
                }
                _client ??= new TcpClient();
                if (!_client.Connected)
                {
                    _logger.LogInformation($"{DeviceId} {DeviceName} Connect {_options.Host} {_options.Port}");
                    await _client.ConnectAsync(_options.Host, _options.Port);
                    _logger.LogInformation($"{DeviceId} {DeviceName} Connected");
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        private void Reset()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
            if (_master != null)
            {
                _master.Dispose();
                _master = null;
            }
            _optionChangesCount = 0;
            _connectionErrorCount = 0;
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public override async ValueTask<bool> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // configure socket
                var serverIP = IPAddress.Parse(_options.Host);
                var serverFullAddr = new IPEndPoint(serverIP, _options.Port);
                var factory = new ModbusFactory();
                IModbusMaster master = factory.CreateMaster(_client);


                var value = await master.ReadHoldingRegistersAsync((byte)0x01, (ushort)0x00, (ushort)0x1);


                var temperature = 0d;
                var humidity = 0d;

                byte slaveId = 1;
                for (int i = 0; i < 2; i++)
                {
                    var startAddress = i;
                    var registers = await master.ReadInputRegistersAsync(slaveId, (ushort)startAddress, 1);
                    switch (i)
                    {
                        case 1:
                            temperature = registers[0] / 10d;
                            break;
                        case 2:
                            humidity = registers[0] / 10d;
                            break;
                        default:
                            break;
                    }
                    await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
                    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                    await dbContext.DeviceDataDbSet.AddAsync(new DeviceDataModel()
                    {
                        DateTime = DateTime.Now,
                        DeviceName = DeviceName,
                        Host = _options.Host,
                        Temperature = temperature,
                        Humidity = humidity,
                    }, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            return true;
        }

        public override ValueTask<bool> UpdateOptionsAsync(JsonElement options)
        {
            return ValueTask.FromResult<bool>(true);
        }
    }
}
