using FluentFTP;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models.Configurations;
using JobsWorkerWebService.Server.Models;
using JobsWorkerWebService.Services.VirtualSystem;
using JobsWorkerWebService.Services.VirtualSystemServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Buffers.Text;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class VirtualFileSystemController : Controller
    {
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig  _virtualFileSystemConfig;

        public VirtualFileSystemController(
            IVirtualFileSystem  virtualFileSystem,
            VirtualFileSystemConfig  virtualFileSystemConfig)
        {
            this._virtualFileSystem = virtualFileSystem;
            this._virtualFileSystemConfig = virtualFileSystemConfig;
        }

        [HttpGet("/api/virtualfilesystem/{**path}")]
        public async Task<IActionResult> DownloadAsync(string path)
        {
            await this._virtualFileSystem.ConnectAsync();
            if (!await this._virtualFileSystem.FileExits(path))
            {
                return NotFound();
            }
            var memoryStream = new MemoryStream();
            if (await this._virtualFileSystem.DownloadStream(path, memoryStream))
            {
                memoryStream.Position = 0;
                return File(memoryStream, "application/octet-stream");
            }
            return NotFound();
        }

        //[ValidateAntiForgeryToken]
        [DisableRequestSizeLimit()]
        [HttpPost("/api/virtualfilesystem/upload/{nodeName}")]
        public async Task<IActionResult> OnPostUploadAsync(string nodeName, List<IFormFile> files)
        {
            ApiResult<UploadFileResult> uploadFileResult = new ApiResult<UploadFileResult>()
            {
                Value = new UploadFileResult()
            };
            try
            {
                uploadFileResult.Value.UploadedFiles = new List<UploadedFile>();
                var nodeCachePath = this._virtualFileSystemConfig.GetFileCachePath(nodeName);
                foreach (var formFile in files)
                {
                    var fileId = formFile.Headers["FileId"];
                    var remotePath = Path.Combine(nodeCachePath, Guid.NewGuid().ToString()).Replace("\\", "/");
                    var downloadUrl = $"{this._virtualFileSystemConfig.RequestUri}/api/virtualfilesystem/{remotePath}";
                    if (await this._virtualFileSystem.UploadStream(
                        remotePath, formFile.OpenReadStream()))
                    {
                        uploadFileResult.Value.UploadedFiles.Add(new UploadedFile()
                        {
                            DownloadUrl = downloadUrl,
                            Name = formFile.FileName,
                            FileId = fileId
                        });
                    }

                }
            }
            catch (Exception ex)
            {
                uploadFileResult.ErrorCode = ex.HResult;
                uploadFileResult.Message = ex.Message;
            }

            return new JsonResult(uploadFileResult);
        }


    }
}
