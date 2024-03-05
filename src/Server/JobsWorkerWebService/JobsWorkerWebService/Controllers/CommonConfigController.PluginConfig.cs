using FluentFTP.Helpers;
using JobsWorker.Shared.DataModels;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace JobsWorkerWebService.Controllers
{
    public class PluginConfigUploadModel : PluginConfigModel
    {
        [NotMapped]
        [JsonIgnore]
        public IFormFile? File { get; set; }
    }

    public partial class CommonConfigController
    {

        [HttpGet("/api/commonconfig/plugin/list")]
        public async Task<ApiResult<IEnumerable<PluginConfigModel>>> QueryPluginConfigsAsync()
        {
            ApiResult<IEnumerable<PluginConfigModel>> apiResult = new ApiResult<IEnumerable<PluginConfigModel>>();
            try
            {
                apiResult.Result = await this._applicationDbContext.PluginConfigDbSet.ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.ToString();
            }
            return apiResult;
        }

        [HttpGet("/api/commonconfig/plugin/download/{pluginId}")]
        public async Task<IActionResult> DownloadPluginAsync(string pluginId)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(pluginId);
            var pluginInfo = await this._applicationDbContext.PluginConfigDbSet.FindAsync(pluginId);
            if (pluginInfo == null)
            {
                return NotFound();
            }
            await this._virtualFileSystem.ConnectAsync();
            var memoryStream = new MemoryStream();
            await this._virtualFileSystem.DownloadStream(pluginInfo.DownloadUrl, memoryStream);
            memoryStream.Position = 0;
            return File(memoryStream, "application/octet-stream", pluginInfo.FileName);
        }

        [HttpPost("/api/commonconfig/plugin/addorupdate")]
        [DisableRequestSizeLimit]
        public async Task<ApiResult<IEnumerable<PluginConfigModel>>> AddOrUpdatePluginAsync([FromForm] PluginConfigUploadModel plugin)
        {
            ApiResult<IEnumerable<PluginConfigModel>> apiResult = new ApiResult<IEnumerable<PluginConfigModel>>();
            try
            {
                ArgumentNullException.ThrowIfNull(plugin.Name);
                ArgumentNullException.ThrowIfNull(plugin.Platform);
                ArgumentNullException.ThrowIfNull(plugin.Version);
                ArgumentNullException.ThrowIfNull(plugin.File);
                ArgumentNullException.ThrowIfNull(plugin.Hash);
                var fileName = Guid.NewGuid().ToString("N");
                var remotePath = Path.Combine(this._virtualFileSystemConfig.GetPluginPath(plugin.Id), fileName);
                await this._virtualFileSystem.ConnectAsync();
                if (!await this._virtualFileSystem.UploadStream(remotePath, plugin.File.OpenReadStream()))
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = "Upload stream fail";
                    return apiResult;
                }
                var pluginConfigFromDb = await this._applicationDbContext.PluginConfigDbSet.FindAsync(plugin.Id);
                if (pluginConfigFromDb == null)
                {
                    var pluginInfo = new PluginConfigModel()
                    {
                        Platform = plugin.Platform,
                        Version = plugin.Version,
                        Arguments = plugin.Arguments,
                        EntryPoint = plugin.EntryPoint,
                        Hash = plugin.Hash,
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Launch = plugin.Launch,
                        DownloadUrl = remotePath,
                        FileName = plugin.File.FileName,
                        FileSize = plugin.File.Length
                    };
                    await this._applicationDbContext.PluginConfigDbSet.AddAsync(pluginInfo);

                }
                else
                {
                    if (pluginConfigFromDb.DownloadUrl != null)
                    {
                        await this._virtualFileSystem.DeleteFileAsync(pluginConfigFromDb.DownloadUrl);
                    }
                    pluginConfigFromDb.With(plugin);
                    pluginConfigFromDb.DownloadUrl = remotePath;
                    pluginConfigFromDb.FileName = plugin.File.FileName;
                    pluginConfigFromDb.FileSize = plugin.File.Length;
                }
                await this._applicationDbContext.SaveChangesAsync();
                apiResult = await this.QueryPluginConfigsAsync();
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.ToString();
            }
            return apiResult;
        }

        [HttpPost("/api/commonconfig/plugin/remove")]
        public async Task<ApiResult<IEnumerable<PluginConfigModel>>> DeletePluginConfigAsync([FromBody] PluginConfigModel[] plugins)
        {

            ApiResult<IEnumerable<PluginConfigModel>> apiResult = new ApiResult<IEnumerable<PluginConfigModel>>();
            try
            {
                this._applicationDbContext.PluginConfigDbSet.RemoveRange(plugins);
                await this._applicationDbContext.SaveChangesAsync();
                await this._virtualFileSystem.ConnectAsync();
                foreach (var plugin in plugins)
                {
                    var pluginInfo = await this._applicationDbContext.PluginConfigDbSet.FindAsync(plugin.Id);
                    if (pluginInfo == null)
                    {
                        continue;
                    }
                    else
                    {
                        var pluginDirectory = this._virtualFileSystemConfig.GetPluginPath(plugin.Id);
                        await this._virtualFileSystem.DeleteDirectoryAsync(pluginDirectory);
                    }
                }
                apiResult = await this.QueryPluginConfigsAsync();

            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.ToString();
            }
            return apiResult;
        }


    }
}
