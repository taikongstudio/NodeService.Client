using FluentFTP;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using JobsWorkerWebService.Extensions;
using JobsWorkerWebService.Models.Configurations;
using JobsWorkerWebService.Services.VirtualSystem;
using JobsWorkerWebService.Services.VirtualSystemServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PluginsController : Controller
    {
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig _virtualFileSystemConfig;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly ILogger<PluginsController> _logger;


        public PluginsController(
            ApplicationDbContext applicationDbContext,
            IVirtualFileSystem  virtualFileSystem,
            VirtualFileSystemConfig  virtualFileSystemConfig,
            ILogger<PluginsController> logger)
        {
            this._applicationDbContext = applicationDbContext;
            this._virtualFileSystem = virtualFileSystem;
            this._virtualFileSystemConfig = virtualFileSystemConfig;
            this._logger = logger;
        }


        [HttpGet("/api/plugins/list")]
        public  Task<IEnumerable<PluginInfo>> ListPlugins()
        {
            return Task.FromResult<IEnumerable<PluginInfo>>(this._applicationDbContext.PluginInfoDbSet);
        }

        [HttpGet("/api/plugins/download/{pluginId}")]
        public async Task<IActionResult> DownloadPlugin(string pluginId)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(pluginId);
            var pluginInfo =await this._applicationDbContext.PluginInfoDbSet.FindAsync(pluginId);
            if (pluginInfo == null)
            {
                return NotFound();
            }
            string remotePath = this._virtualFileSystemConfig.GetPluginPath(pluginId);
            return File(await this._virtualFileSystem.ReadFileAsync(remotePath), "application/octet-stream");
        }

        // POST: PluginsController/Create
        [HttpPost("/api/plugins/upload/")]
        [DisableRequestSizeLimit]
        public async Task<ActionResult> UploadPlugin(
            [FromHeader(Name = "JobsWorkerWebService-PluginName")] string pluginName,
            [FromHeader(Name = "JobsWorkerWebService-Platform")] string platform,
            [FromHeader(Name = "JobsWorkerWebService-Version")] string version,
            [FromHeader(Name = "JobsWorkerWebService-Arguments")] string arguments,
            [FromHeader(Name = "JobsWorkerWebService-EntryPoint")] string entryPoint,
            [FromHeader(Name = "JobsWorkerWebService-Hash")] string hash,
            [FromHeader(Name = "JobsWorkerWebService-Launch")] string launch,
            IFormFile file)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(pluginName);
                ArgumentNullException.ThrowIfNull(platform);
                ArgumentNullException.ThrowIfNull(version);
                ArgumentNullException.ThrowIfNull(file);
                ArgumentNullException.ThrowIfNull(hash);
                var pluginId = Guid.NewGuid().ToString("N");
                var fileName = Guid.NewGuid().ToString("N");
                var remotePath = Path.Combine(this._virtualFileSystemConfig.GetPluginPath(pluginId), fileName);
                await this._virtualFileSystem.ConnectAsync();
                if (await this._virtualFileSystem.UploadStream(remotePath, file.OpenReadStream()))
                {
                    var pluginInfo = new PluginInfo()
                    {
                        platform = platform,
                        version = version,
                        arguments = arguments,
                        entryPoint = entryPoint,
                        hash = hash,
                        pluginId = pluginId,
                        pluginName = pluginName,
                        launch = bool.TrueString == launch,
                        downloadUrl = remotePath
                    };
                    this._applicationDbContext.PluginInfoDbSet.Add(pluginInfo);
                    await this._applicationDbContext.SaveChangesAsync();
                    return new JsonResult(new ApiResult()
                    {
                        ErrorCode = 0,
                        Message = "Upload success"
                    });
                }
                return new JsonResult(new ApiResult()
                {
                    ErrorCode = -1,
                    Message = "Upload fail"
                });
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex.ToString());
                return new JsonResult(new ApiResult
                {
                    ErrorCode = ex.HResult,
                    Message = ex.ToString()
                });
            }

        }

        [HttpPost("/api/plugins/delete")]
        public async Task<ApiResult> DeletePlugin([FromQuery] string pluginId)
        {
            ApiResult apiResult = new ApiResult();
            try
            {
                var pluginInfo = await this._applicationDbContext.PluginInfoDbSet.FindAsync(pluginId);
                if (pluginInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = "plugin not exits";
                    return apiResult;
                }
                else
                {
                    var pluginDirectory = this._virtualFileSystemConfig.GetPluginPath(pluginId);
                    await this._virtualFileSystem.ConnectAsync();
                    await this._virtualFileSystem.DeleteDirectoryAsync(pluginDirectory);
                    this._applicationDbContext.PluginInfoDbSet.Remove(pluginInfo);
                    await this._applicationDbContext.SaveChangesAsync();
                    apiResult.ErrorCode = 0;
                    apiResult.Message = "delete plugin success";
                }
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
