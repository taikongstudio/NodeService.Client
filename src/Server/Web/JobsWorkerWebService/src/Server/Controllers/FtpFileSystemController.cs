using JobsWorkerWebService.Server.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace JobsWorkerWebService.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FtpFileSystemController : ControllerBase
    {
        private FileSystemConfig _fileSystemConfig;
        public FtpFileSystemController(FileSystemConfig fileSystemConfig)
        {
            this._fileSystemConfig = fileSystemConfig;
        }

        public IEnumerable<string> Index()
        {
            return Directory.GetDirectories(this._fileSystemConfig.RootPath);
        }

        [HttpGet("/api/ftpfilesystem/{**path}")]
        public IActionResult Index(string path, [FromQuery]string? searchpattern)
        {
            var realPath = Path.Combine(this._fileSystemConfig.RootPath, path);
            if (Directory.Exists(realPath))
            {
                List<Models.FileSystemInfo> fileSystemObjectList = new List<Models.FileSystemInfo>();
                DirectoryInfo directoryInfo = new DirectoryInfo(realPath);
                foreach (var item in directoryInfo.GetFileSystemInfos())
                {
                    fileSystemObjectList.Add(new Models.FileSystemInfo()
                    {
                        Name = item.Name,
                        FullName = item.FullName,
                        LastWriteTime = item.LastWriteTime,
                        CreationTime = item.CreationTime,
                        Length = item.Attributes.HasFlag(FileAttributes.Directory) ? 0 : (item as FileInfo).Length,
                        Type = item.Attributes.HasFlag(FileAttributes.Directory) ? "directory" : "file"
                    });
                }
                return new JsonResult(fileSystemObjectList);
            }
            else if (System.IO.File.Exists(realPath))
            {
                FileInfo fileInfo = new FileInfo(realPath);
                Models.FileSystemInfo fileSystemObject = new Models.FileSystemInfo()
                {
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    LastWriteTime = fileInfo.LastWriteTime,
                    CreationTime = fileInfo.CreationTime,
                    Length = fileInfo.Length,
                    Type = "file"
                };
                return new JsonResult(fileSystemObject);
            }
            return NotFound();
        }


    }
}
