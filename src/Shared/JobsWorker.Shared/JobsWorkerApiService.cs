using JobsWorker.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared
{
    public class JobsWorkerApiService:IDisposable
    {
        private HttpClient _httpClient;

        public RestApi[] RestApis { get; private set; }

        public string BaseAddress {  get; private set; }

        public JobsWorkerApiService(string baseAddress, RestApi[] apis)
        {
            this.RestApis = apis;
            this.BaseAddress = baseAddress;
            this._httpClient = new HttpClient()
            {
                BaseAddress = new Uri(baseAddress)
            };
        }

        public async Task<IEnumerable<NodeInfo>> QueryDeviceListAsync()
        {
            return await this._httpClient.GetFromJsonAsync<NodeInfo[]>("/api/devices/list");
        }

        public  Task<Stream> DownloadPluginFileAsync(PluginInfo pluginInfo)
        {
            return Task.FromResult<Stream>(null);
        }

        public void Dispose()
        {
            this._httpClient.Dispose();
            this._httpClient = null;
        }
    }
}
