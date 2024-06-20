using Microsoft.Extensions.Hosting;
using NModbus;
using NModbus.Extensions.Enron;
using NodeService.Infrastructure;
using NodeService.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NodeService.DeviceHost.Devices
{
    public class YinHeDeviceOptions
    {
        public string Host { get; set; }

        public int Port { get; set; }

    }

    public class YinHeDevice : Device
    {
        private readonly ILogger<YinHeDevice> _logger;
        readonly YinHeDeviceOptions _options;
        readonly ModbusFactory _factory;
        int _optionChangesCount;
        int _connectionErrorCount;
        TcpClient _client;
        IModbusMaster _master;

        public YinHeDevice(ILogger<YinHeDevice> logger,YinHeDeviceOptions options)
        {
            _logger = logger;
            _options = options;
            _factory = new ModbusFactory();
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

        public override async ValueTask<bool> FetchDataAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _master ??= _factory.CreateMaster(_client);
                ushort startAddress = (ushort)0x00A0;
                var registers = await _master.ReadHoldingRegisters32Async(0, startAddress, 2);
                var temperature = BitConverter.UInt32BitsToSingle(registers[0]);
                var humidity = BitConverter.UInt32BitsToSingle(registers[0]);
                _logger.LogInformation($"temperature:{temperature}");
                _logger.LogInformation($"humidity:{humidity}");
                return true;
            }
            catch(SocketException ex)
            {
                _logger.LogError($"{DeviceId} {DeviceName} {ex}");
                _connectionErrorCount++;
            }
            catch(IOException ex)
            {
                _logger.LogError($"{DeviceId} {DeviceName} {ex}");
                _connectionErrorCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"{DeviceId} {DeviceName} {ex}");
            }

            return false;
        }

        public override ValueTask<bool> UpdateOptionsAsync(JsonElement options)
        {
            var hostPortOptions = options.Deserialize<HostPortSettings>();
            if (hostPortOptions == null)
            {
                return ValueTask.FromResult(true);
            }
            if (_options.Host != hostPortOptions.IpAddress)
            {
                _logger.LogInformation($"{DeviceId} {DeviceName} Update host from {_options.Host} to {hostPortOptions.IpAddress}");
                _options.Host = hostPortOptions.IpAddress;
                _optionChangesCount++;
            }
            if (_options.Port != hostPortOptions.Port)
            {
                _logger.LogInformation($"{DeviceId} {DeviceName} Update port from {_options.Port} to {hostPortOptions.Port}");
                _options.Port = hostPortOptions.Port;
                _optionChangesCount++;
            }
            return ValueTask.FromResult(true);
        }

    }
}
