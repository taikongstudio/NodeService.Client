using FluentFTP;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Models;
using JobsWorkerWebService.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Buffers.Text;

namespace JobsWorkerWebService.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class FilesController : Controller
    {
        private readonly AsyncFtpClient _asyncFtpClient;
        private readonly FtpServerConfig _ftpServerConfig;

        public FilesController(AsyncFtpClient asynFtpClient, FtpServerConfig ftpServerConfig)
        {
            this._asyncFtpClient = asynFtpClient;
            this._ftpServerConfig = ftpServerConfig;
        }

        //[ValidateAntiForgeryToken]
        [RequestSizeLimit(1024 * 1024 * 1024)]
        [HttpPost("/api/files/upload/{nodeName}")]
        public async Task<IActionResult> OnPostUploadAsync(string nodeName, List<IFormFile> files)
        {
            Result<UploadFileResult> uploadFileResult = new Result<UploadFileResult>()
            {
                Value = new UploadFileResult()
            };
            try
            {
                uploadFileResult.Value.UploadedFiles = new List<UploadedFile>();
                var machineRootPath = string.Format(this._ftpServerConfig.nodeServiceConfigDir, nodeName);
                foreach (var formFile in files)
                {
                    var reletivePath = Path.Combine(machineRootPath, Guid.NewGuid().ToString()).Replace("\\", "/");
                    var remotePath = Path.Combine(machineRootPath, reletivePath).Replace("\\", "/");
                    var downloadUrl = $"{this.HttpContext.Request.Scheme}{this.HttpContext.Request.Host}/api/ftpfilesystem/{nodeName}/{reletivePath}";
                    var status = await this._asyncFtpClient.UploadStream(formFile.OpenReadStream(),
                        remotePath,
                        FtpRemoteExists.Overwrite,
                        true);
                    if (status != FtpStatus.Failed)
                    {
                        uploadFileResult.Value.UploadedFiles.Add(new UploadedFile()
                        {
                            DownloadUrl = downloadUrl,
                            Name = formFile.FileName,
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
