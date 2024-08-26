
using Microsoft.Extensions.Options;
using NodeService.DeviceHost.Devices;
using NodeService.DeviceHost.Models;
using NodeService.Infrastructure;
using NodeService.Infrastructure.DataModels;
using NodeService.Infrastructure.Models;
using NodeService.ServiceHost.Models;
using System.Collections.Concurrent;

namespace NodeService.DeviceHost.Services
{
    public class DeviceService : BackgroundService
    {
        readonly ILogger<DeviceService> _logger;
        readonly IHttpClientFactory _httpClientFactory;
        private readonly DeviceFactory _deviceFactory;
        readonly IDisposable _serverOptionMonitorToken;
        private readonly ServiceOptions _serviceOptions;
        readonly ConcurrentDictionary<string, Device> _devicesDictionary;
        ServerOptions _serverOptions;
        ApiService _apiService;

        public DeviceService(
            ILogger<DeviceService> logger,
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<ServerOptions> serverOptionsMonitor,
            DeviceFactory deviceFactory,
            ServiceOptions serviceOptions
            )
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _deviceFactory = deviceFactory;
            _devicesDictionary = new ConcurrentDictionary<string, Device>();
            OnServerOptionChanged(serverOptionsMonitor.CurrentValue);
            _serverOptionMonitorToken = serverOptionsMonitor.OnChange(OnServerOptionChanged);
            _serviceOptions = serviceOptions;
        }

        public override void Dispose()
        {
            _serverOptionMonitorToken?.Dispose();
            base.Dispose();
        }

        void OnServerOptionChanged(ServerOptions serverOptions)
        {
            _serverOptions = serverOptions;
        }

        ApiService CreateApiService()
        {
            var apiService = new ApiService(_httpClientFactory.CreateClient());
            apiService.HttpClient.BaseAddress = new Uri(_serverOptions.HttpAddress);
            apiService.HttpClient.Timeout = TimeSpan.FromMinutes(5);
            return apiService;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            Task[] tasks =
                [
                    RefreshDeviceListAsync(cancellationToken),
                    ExecuteDeviceListAsync(cancellationToken)
                ];
            await Task.WhenAny(tasks);
        }

        async Task ExecuteDeviceListAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        if (_devicesDictionary.IsEmpty)
                        {
                            continue;
                        }
                        await Parallel.ForEachAsync(_devicesDictionary.Values, new ParallelOptions()
                        {
                            CancellationToken = cancellationToken,
                            MaxDegreeOfParallelism = 8,
                        }, DeviceWorkAsync);
                    }
                    catch (Exception ex)
                    {

                    }
                    finally
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {

            }


        }

        private async ValueTask DeviceWorkAsync(Device device, CancellationToken cancellationToken = default)
        {
            if (device == null)
            {
                return;
            }
            await device.ConnectAsync();
            await device.FetchDataAsync(cancellationToken);
        }

        async Task RefreshDeviceListAsync(CancellationToken cancellationToken = default)
        {
            using var apiService = CreateApiService();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Begin query network devices");
                    var networkDeviceList = await QueryNetworkDeviceListAsync(apiService, cancellationToken);
                    _logger.LogInformation("Finish query network devices");
                    if (networkDeviceList.Count < _devicesDictionary.Count)
                    {
                        var deviceToRemoveList = networkDeviceList.Select(static x => x.Id).Except(_devicesDictionary.Keys);
                        foreach (var id in deviceToRemoveList)
                        {
                            _devicesDictionary.TryRemove(id, out _);
                        }
                    }

                    foreach (var networkDevice in networkDeviceList)
                    {
                        if (_devicesDictionary.TryGetValue(networkDevice.Id, out Device? device))
                        {
                            await _deviceFactory.TryUpdateDeviceOptionsAsync(
                                apiService,
                                device,
                                networkDevice,
                                cancellationToken);
                            continue;
                        }
                        device = await _deviceFactory.TryCreateDeviceAsync(
                            apiService,
                            networkDevice,
                            cancellationToken);
                        if (device != null)
                        {
                            _devicesDictionary.TryAdd(networkDevice.Id, device);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }


        }

        async Task<List<NodeInfoModel>> QueryNetworkDeviceListAsync(ApiService apiService, CancellationToken cancellationToken)
        {
            int pageIndex = 1;
            int pageSize = 100;
            List<NodeInfoModel> nodeList = [];
            while (true)
            {
                _logger.LogInformation($"Query {pageIndex} page, page size {pageSize}");
                var rsp = await apiService.QueryNodeListAsync(new QueryNodeListParameters()
                {
                    Status = NodeStatus.Online,
                    AreaTag = AreaTags.Any,
                    DeviceType = NodeDeviceType.NetworkDevice,
                    IncludeProperties = true,
                    PageIndex = pageIndex,
                    PageSize = pageSize
                }, cancellationToken);
                
                if (rsp.ErrorCode == 0)
                {
                    nodeList.AddRange(rsp.Result);
                }
                else
                {
                    _logger.LogInformation($"Query {pageIndex} page, page size {pageSize},ErrorCode:{rsp.ErrorCode},Message:{rsp.Message}");
                    break;
                }
                if (rsp.Result.Count() < pageSize)
                {
                    break;
                }
                pageIndex++;

            }
            return nodeList;
        }
    }
}
