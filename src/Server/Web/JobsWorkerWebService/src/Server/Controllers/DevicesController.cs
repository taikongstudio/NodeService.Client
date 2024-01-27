
using JobsWorkerWebService.Server.Data;
using JobsWorkerWebService.Server.FileSystemServices;
using JobsWorkerWebService.Server.Models;
using JobsWorkerWebService.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobsWorkerWebService.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class DevicesController : Controller
    {
        private readonly MachineInfoDbContext _machineInfoDbContext;
        private readonly IInprocRpc<string, FileSystemRequest, FileSystemResponse> _inprocessMessageQueue;

        public DevicesController(
            MachineInfoDbContext machineInfoDbContext,
            IInprocRpc<string, FileSystemRequest, FileSystemResponse> inprocessMessageQueue)
        {
            this._machineInfoDbContext = machineInfoDbContext;
            this._inprocessMessageQueue = inprocessMessageQueue;
        }

        [HttpGet("/api/devices/{machineName}/config/server.bat")]
        public FtpServerConfig GetDeviceServerConfig(string machineName)
        {
            return new FtpServerConfig();
        }

        [HttpGet("/api/devices/{machineName}/plugins/{pluginName}/config/server.bat")]
        public FtpServerConfig GetPluginServerConfig(string machineName, string pluginName)
        {
            return new FtpServerConfig();
        }


        [HttpGet("/api/devices/{machineName}/plugins/{pluginName}/config/{version}/config.bat")]
        public JobScheduleConfig[] GetPluginConfig(string machineName, string pluginName, string version)
        {
            return Array.Empty<JobScheduleConfig>();
        }

        [HttpGet("/api/devices/{machineName}/filesystem/{**path}")]
        public async Task<FileSystemListResult> List(string machineName, string path, [FromQuery] string? searchpattern)
        {
            FileSystemListResult fileSystemListResult = new FileSystemListResult();
            try
            {

                FileSystemListRequest fileSystemListRequest = new FileSystemListRequest();
                fileSystemListRequest.Path = path;
                fileSystemListRequest.SearchPattern = searchpattern;
                fileSystemListRequest.Timeout = 60000;
                FileSystemListResponse rsp =
                    await this._inprocessMessageQueue.SendRequestAsync<FileSystemListResponse>(machineName, fileSystemListRequest);
                fileSystemListResult.ErrorCode = rsp.ErrorCode;
                fileSystemListResult.ErrorMessage = rsp.ErrorMessage;
                fileSystemListResult.Items = rsp.FileSystemObjects.Select(x => new Models.FileSystemInfo()
                {
                    FullName = x.FullName,
                    Name = x.Name,
                    CreationTime = x.CreationTime.ToDateTime(),
                    LastWriteTime = x.LastWriteTime.ToDateTime(),
                    Length = x.Length,
                    Type = x.Type,
                });
            }
            catch (Exception ex)
            {
                fileSystemListResult.ErrorCode = ex.HResult;
                fileSystemListResult.ErrorMessage = ex.Message;
            }
            return fileSystemListResult;
        }

        [HttpGet("/api/devices/{machineName}/filesystem/")]
        public IEnumerable<string> DeviceFileSystemRoot(string machineName)
        {
            return Directory.GetDirectories("/");
        }

        [HttpGet("/api/devices/list")]
        public IEnumerable<MachineInfo> List()
        {
            return this._machineInfoDbContext.MachineInfos;
        }

        // GET: DevicesController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: DevicesController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: DevicesController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(GetPluginServerConfig));
            }
            catch
            {
                return View();
            }
        }

        // GET: DevicesController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: DevicesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(GetPluginServerConfig));
            }
            catch
            {
                return View();
            }
        }

        // GET: DevicesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: DevicesController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(GetPluginServerConfig));
            }
            catch
            {
                return View();
            }
        }
    }
}
