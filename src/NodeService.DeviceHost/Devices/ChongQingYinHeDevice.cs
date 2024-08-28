using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using NModbus;
using NModbus.Extensions.Enron;
using NodeService.DeviceHost.Data;
using NodeService.DeviceHost.Data.Models;
using NodeService.DeviceHost.Models;
using NodeService.Infrastructure.Models;
using System.Net.Sockets;
using System.Text.Json;

namespace NodeService.DeviceHost.Devices
{
    public class YinHeDeviceOptions
    {
        public string Host { get; set; }

        public int Port { get; set; }

        public int SamplingDurationInSeconds { get; set; }
    }

    public class ChongQingYinHeDevice : Device
    {
        JsonSerializerOptions _jsonOptions = new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
        };

        readonly ILogger<ChongQingYinHeDevice> _logger;
        readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
        readonly YinHeDeviceOptions _options;
        readonly ModbusFactory _factory;
        private readonly ServiceOptions _serviceOptions;
        int _optionChangesCount;
        int _connectionErrorCount;
        TcpClient _client;
        IModbusMaster _master;
        DateTime _lastSamplingDateTime;

        public ChongQingYinHeDevice(
            ILogger<ChongQingYinHeDevice> logger,
            IDbContextFactory<ApplicationDbContext> dbContextFactory,
            YinHeDeviceOptions options,
            ServiceOptions serviceOptions)
        {
            _logger = logger;
            _dbContextFactory = dbContextFactory;
            _options = options;
            _factory = new ModbusFactory();
            _serviceOptions = serviceOptions;
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
                if (DateTime.Now - _lastSamplingDateTime < TimeSpan.FromSeconds(_options.SamplingDurationInSeconds))
                {
                    return false;
                }
                _lastSamplingDateTime = DateTime.Now;
                _master ??= _factory.CreateMaster(_client);
                ushort startAddress = (ushort)0x00A0;
                var registers = await _master.ReadHoldingRegisters32Async(0, startAddress, 2);
                var temperature = BitConverter.UInt32BitsToSingle(registers[0]);
                var humidity = BitConverter.UInt32BitsToSingle(registers[1]);
                if (_serviceOptions.verbs != 0)
                {
                    _logger.LogInformation($"temperature:{temperature}");
                    _logger.LogInformation($"humidity:{humidity}");
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

            var hostPortOptions = options.Deserialize<HostPortSettings>(_jsonOptions);
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

            _options.SamplingDurationInSeconds = hostPortOptions.SamplingDurationInSeconds;

            return ValueTask.FromResult(true);
        }

        public override ValueTask DisposeAsync()
        {
            _client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
