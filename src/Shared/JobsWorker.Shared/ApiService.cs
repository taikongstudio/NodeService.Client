using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorker.Shared
{
    public class ApiService : IDisposable
    {
        public HttpClient HttpClient { get; private set; }

        public string BaseAddress { get; private set; }

        public ApiService(HttpClient httpClient)
        {
            this.HttpClient = httpClient;
        }

        private Task<ApiResult<IEnumerable<T>>?> GetListAsync<T>(string requestUri)
        {
            return this.HttpClient.GetFromJsonAsync<ApiResult<IEnumerable<T>>>(requestUri);
        }

        private Task<ApiResult<T>?> GetAsync<T>(string requestUri)
        {
            return this.HttpClient.GetFromJsonAsync<ApiResult<T>>(requestUri);
        }
        private async Task<ApiResult<IEnumerable<T>>?> PostListAsync<T>(string requestUri, T[] models) where T : class
        {
            var rsp = await this.HttpClient.PostAsJsonAsync<T[]>(requestUri, models);
            var value = await rsp.Content.ReadAsStringAsync();
            rsp.EnsureSuccessStatusCode();
            return await rsp.Content.ReadFromJsonAsync<ApiResult<IEnumerable<T>>>();
        }

        private async Task<ApiResult<TResult>?> PostAsync<TValue, TResult>(string requestUri, TValue model) where TValue : class
        {
            var rsp = await this.HttpClient.PostAsJsonAsync<TValue>(requestUri, model);
            var value = await rsp.Content.ReadAsStringAsync();
            rsp.EnsureSuccessStatusCode();
            return await rsp.Content.ReadFromJsonAsync<ApiResult<TResult>>();
        }


        public Task<ApiResult<IEnumerable<NodeInfoModel>>?> QueryNodeListAsync()
        {
            return GetListAsync<NodeInfoModel>("/api/nodes/list");
        }

        public Task<ApiResult<NodeInfoModel>?> QueryNodeInfoAsync(string id)
        {
            return GetAsync<NodeInfoModel>($"/api/nodes/{id}");
        }

        public Task<ApiResult<IEnumerable<NodePropertyEntry>>?> QueryNodePropsAsync(string id)
        {
            return GetListAsync<NodePropertyEntry>($"/api/nodes/{id}/props/list");
        }

        public Task<ApiResult<IEnumerable<PluginConfigModel>>?> QueryPluginConfigListAsync()
        {
            return GetListAsync<PluginConfigModel>("/api/commonconfig/plugin/list");
        }

        public async Task<ApiResult<IEnumerable<PluginConfigModel>>?> AddOrUpdateAsync(PluginConfigModel pluginConfig, Stream stream)
        {
            HttpRequestMessage BuildRequestMessage()
            {
                string requestUri = $"/api/commonconfig/plugin/addorupdate";
                var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
                var requestContent = new MultipartFormDataContent();
                var streamContent = new StreamContent(stream);
                requestContent.Add(streamContent, "File", "File");
                requestContent.Add(new StringContent(pluginConfig.Id), nameof(PluginConfigModel.Id));
                requestContent.Add(new StringContent(pluginConfig.Name), nameof(PluginConfigModel.Name));
                requestContent.Add(new StringContent(pluginConfig.Launch.ToString()), nameof(PluginConfigModel.Launch));
                requestContent.Add(new StringContent(pluginConfig.Version), nameof(PluginConfigModel.Version));
                requestContent.Add(new StringContent(pluginConfig.EntryPoint ?? string.Empty), nameof(PluginConfigModel.EntryPoint));
                requestContent.Add(new StringContent(pluginConfig.Arguments ?? string.Empty), nameof(PluginConfigModel.Arguments));
                requestContent.Add(new StringContent(pluginConfig.Platform), nameof(PluginConfigModel.Platform));
                requestContent.Add(new StringContent(pluginConfig.Hash ?? string.Empty), nameof(PluginConfigModel.Hash));
                requestContent.Add(new StringContent(pluginConfig.FileName ?? string.Empty), nameof(PluginConfigModel.FileName));
                requestContent.Add(new StringContent(pluginConfig.FileSize.ToString()), nameof(PluginConfigModel.FileSize));
                requestMessage.Content = requestContent;
                return requestMessage;
            }
            using var httpRequestMessage = BuildRequestMessage();
            var rsp = await this.HttpClient.SendAsync(httpRequestMessage);
            var value = await rsp.Content.ReadAsStringAsync();
            rsp.EnsureSuccessStatusCode();
            return await rsp.Content.ReadFromJsonAsync<ApiResult<IEnumerable<PluginConfigModel>>>();
        }

        public Task<ApiResult<IEnumerable<PluginConfigModel>>?> RemoveAsync(PluginConfigModel[] pluginConfigs)
        {
            return PostListAsync("/api/commonconfig/plugin/remove", pluginConfigs);
        }

        public Task<ApiResult<IEnumerable<FtpConfigModel>>?> QueryFtpConfigListAsync()
        {
            return GetListAsync<FtpConfigModel>("/api/commonconfig/ftp/list");
        }

        public Task<ApiResult<IEnumerable<FtpConfigModel>>?> AddOrUpdateAsync(FtpConfigModel[] ftpConfigs)
        {
            return PostListAsync<FtpConfigModel>("/api/commonconfig/ftp/addorupdate", ftpConfigs);
        }

        public Task<ApiResult<IEnumerable<FtpConfigModel>>?> RemoveAsync(FtpConfigModel[] ftpConfigs)
        {
            return PostListAsync<FtpConfigModel>("/api/commonconfig/ftp/remove", ftpConfigs);
        }

        public Task<ApiResult<IEnumerable<MysqlConfigModel>>?> QueryMysqlConfigListAsync()
        {
            return GetListAsync<MysqlConfigModel>("/api/commonconfig/mysql/list");
        }

        public Task<ApiResult<IEnumerable<MysqlConfigModel>>?> AddOrUpdateAsync(MysqlConfigModel[] mysqlConfigs)
        {
            return PostListAsync<MysqlConfigModel>("/api/commonconfig/mysql/addorupdate", mysqlConfigs);
        }

        public Task<ApiResult<IEnumerable<MysqlConfigModel>>?> RemoveAsync(MysqlConfigModel[] mysqlConfigs)
        {
            return PostListAsync<MysqlConfigModel>("/api/commonconfig/mysql/remove", mysqlConfigs);
        }

        public Task<ApiResult<IEnumerable<FtpUploadConfigModel>>?> QueryFtpUploadConfigListAsync()
        {
            return GetListAsync<FtpUploadConfigModel>("/api/commonconfig/ftpupload/list");
        }

        public Task<ApiResult<IEnumerable<FtpUploadConfigModel>>?> AddOrUpdateAsync(FtpUploadConfigModel[] ftpUploadConfigs)
        {
            return PostListAsync<FtpUploadConfigModel>("/api/commonconfig/ftpupload/addorupdate", ftpUploadConfigs);
        }

        public Task<ApiResult<IEnumerable<FtpUploadConfigModel>>?> RemoveAsync(FtpUploadConfigModel[] ftpUploadConfigs)
        {
            return PostListAsync<FtpUploadConfigModel>("/api/commonconfig/ftpupload/remove", ftpUploadConfigs);
        }

        public Task<ApiResult<IEnumerable<JobScheduleConfigModel>>?> QueryTaskScheduleConfigListAsync()
        {
            return GetListAsync<JobScheduleConfigModel>("/api/commonconfig/taskschedule/list");
        }

        public Task<ApiResult<IEnumerable<JobScheduleConfigModel>>?> AddOrUpdateAsync(JobScheduleConfigModel[] taskScheduleConfigs)
        {
            return PostListAsync<JobScheduleConfigModel>("/api/commonconfig/taskschedule/addorupdate", taskScheduleConfigs);
        }

        public Task<ApiResult<IEnumerable<JobScheduleConfigModel>>?> RemoveAsync(JobScheduleConfigModel[] taskScheduleConfigs)
        {
            return PostListAsync<JobScheduleConfigModel>("/api/commonconfig/taskschedule/remove", taskScheduleConfigs);
        }

        public Task<ApiResult<IEnumerable<LogUploadConfigModel>>?> QueryLogUploadConfigListAsync()
        {
            return GetListAsync<LogUploadConfigModel>("/api/commonconfig/logupload/list");
        }

        public Task<ApiResult<IEnumerable<LogUploadConfigModel>>?> AddOrUpdateAsync(LogUploadConfigModel[] logUploadConfigs)
        {
            return PostListAsync<LogUploadConfigModel>("/api/commonconfig/logupload/addorupdate", logUploadConfigs);
        }

        public Task<ApiResult<IEnumerable<LogUploadConfigModel>>?> RemoveAsync(LogUploadConfigModel[] logUploadConfigs)
        {
            return PostListAsync<LogUploadConfigModel>("/api/commonconfig/logupload/remove", logUploadConfigs);
        }

        public Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>?> QueryNodeConfigTemplateListAsync()
        {
            return GetListAsync<NodeConfigTemplateModel>("/api/commonconfig/nodeconfigtemplates/list");
        }

        public Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>?> AddOrUpdateAsync(NodeConfigTemplateModel[] nodeConfigTemplates)
        {
            return PostListAsync<NodeConfigTemplateModel>("/api/commonconfig/nodeconfigtemplates/addorupdate", nodeConfigTemplates);
        }

        public Task<ApiResult<IEnumerable<NodeConfigTemplateModel>>?> RemoveAsync(NodeConfigTemplateModel[] nodeConfigTemplates)
        {
            return PostListAsync<NodeConfigTemplateModel>("/api/commonconfig/nodeconfigtemplates/remove", nodeConfigTemplates);
        }

        public Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>?> QueryLocalDirectoryMappingConfigListAsync()
        {
            return GetListAsync<LocalDirectoryMappingConfigModel>("/api/commonconfig/localdirectorymapping/list");
        }

        public Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>?> AddOrUpdateAsync(LocalDirectoryMappingConfigModel[] nodeConfigTemplates)
        {
            return PostListAsync<LocalDirectoryMappingConfigModel>("/api/commonconfig/localdirectorymapping/addorupdate", nodeConfigTemplates);
        }

        public Task<ApiResult<IEnumerable<LocalDirectoryMappingConfigModel>>?> RemoveAsync(LocalDirectoryMappingConfigModel[] nodeConfigTemplates)
        {
            return PostListAsync<LocalDirectoryMappingConfigModel>("/api/commonconfig/localdirectorymapping/remove", nodeConfigTemplates);
        }
        public Task<ApiResult<IEnumerable<RestApiConfigModel>>?> QueryRestApiConfigListAsync()
        {
            return GetListAsync<RestApiConfigModel>("/api/commonconfig/restapi/list");
        }

        public Task<ApiResult<IEnumerable<RestApiConfigModel>>?> AddOrUpdateAsync(RestApiConfigModel[] restApiConfigs)
        {
            return PostListAsync<RestApiConfigModel>("/api/commonconfig/restapi/addorupdate", restApiConfigs);
        }

        public Task<ApiResult<IEnumerable<RestApiConfigModel>>?> RemoveAsync(RestApiConfigModel[] restApiConfigs)
        {
            return PostListAsync<RestApiConfigModel>("/api/commonconfig/restapi/remove", restApiConfigs);
        }

        public Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>?> QueryJobTypeDescConfigListAsync()
        {
            return GetListAsync<JobTypeDescConfigModel>("/api/commonconfig/JobTypedesc/list");
        }

        public Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>?> AddOrUpdateAsync(JobTypeDescConfigModel[] JobTypeDescConfigs)
        {
            return PostListAsync<JobTypeDescConfigModel>("/api/commonconfig/JobTypedesc/addorupdate", JobTypeDescConfigs);
        }

        public Task<ApiResult<IEnumerable<JobTypeDescConfigModel>>?> RemoveAsync(JobTypeDescConfigModel[] JobTypeDescConfigs)
        {
            return PostListAsync<JobTypeDescConfigModel>("/api/commonconfig/JobTypedesc/remove", JobTypeDescConfigs);
        }

        public Task<ApiResult<IEnumerable<KafkaConfigModel>>?> QueryKafkaConfigListAsync()
        {
            return GetListAsync<KafkaConfigModel>("/api/commonconfig/kafka/list");
        }

        public Task<ApiResult<IEnumerable<KafkaConfigModel>>?> AddOrUpdateAsync(KafkaConfigModel[] kafkaConfigs)
        {
            return PostListAsync<KafkaConfigModel>("/api/commonconfig/kafka/addorupdate", kafkaConfigs);
        }

        public Task<ApiResult<IEnumerable<KafkaConfigModel>>?> RemoveAsync(KafkaConfigModel[] kafkaConfigs)
        {
            return PostListAsync<KafkaConfigModel>("/api/commonconfig/kafka/remove", kafkaConfigs);
        }

        public Task<ApiResult<IEnumerable<JobExecutionInstanceModel>>?> QueryNodeJobExecutionInstancesAsync(string nodeId, string jobId)
        {
            return GetListAsync<JobExecutionInstanceModel>($"/api/nodes/{nodeId}/jobs/{jobId}/instances/list");
        }

        public Task<ApiResult<IEnumerable<JobExecutionInstanceModel>>?> QueryAllJobExecutionInstancesAsync(DateTime? startDateTime = null, DateTime? endDateTime = null, int? pageSize = null, int? pageIndex = null)
        {
            return GetListAsync<JobExecutionInstanceModel>($"/api/jobs/instances/list?startdatetime={startDateTime}&enddatetime={endDateTime}&pagesize={pageSize}&pageindex={pageIndex}");
        }

        public Task<ApiResult<bool>?> UpdateNodeProfileAsync(UpdateNodeProfileModel updateNodeInfoModel)
        {
            return PostAsync<UpdateNodeProfileModel, bool>($"/api/nodes/{updateNodeInfoModel.NodeId}/profile/update", updateNodeInfoModel);
        }

        //public Task<ApiResult<FtpConfigModel?>> GetFtpConfigAsync(string id)
        //{
        //    return GetAsync<FtpConfigModel>($"/api/commonconfig/ftp/{id}");
        //}

        //public Task<ApiResult<FtpConfigModel?>> GetMysqlConfigAsync(string id)
        //{
        //    return GetAsync<FtpConfigModel>($"/api/commonconfig/mysql/{id}");
        //}

        //public Task<ApiResult<FtpConfigModel?>> GetKafkaConfigAsync(string id)
        //{
        //    return GetAsync<FtpConfigModel>($"/api/commonconfig/kafka/{id}");
        //}

        //public Task<ApiResult<FtpConfigModel?>> GetLocalDirectoryMappingConfigAsync(string id)
        //{
        //    return GetAsync<FtpConfigModel>($"/api/commonconfig/localdirectorymapping/{id}");
        //}

        //public Task<ApiResult<FtpUploadConfigModel?>> GetFtpUploadConfigAsync(string id)
        //{
        //    return GetAsync<FtpUploadConfigModel>($"/api/commonconfig/ftpupload/{id}");
        //}

        //public Task<ApiResult<LogUploadConfigModel?>> GetFtpUploadConfigAsync(string id)
        //{
        //    return GetAsync<LogUploadConfigModel>($"/api/commonconfig/logupload/{id}");
        //}

        public void Dispose()
        {
            this.HttpClient.Dispose();
        }
    }
}
