using FluentFTP;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JobsWorkerWebService.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PluginsController : Controller
    {
        private readonly AsyncFtpClient _asyncFtpClient;
        private PluginInfoDbContext _pluginInfoDbContext;
        private ILogger<PluginsController> _logger;


        public PluginsController(PluginInfoDbContext pluginInfoDbContext, ILogger<PluginsController> logger)
        {
            this._pluginInfoDbContext = pluginInfoDbContext;
            this._logger = logger;
        }


        [HttpGet("/plugins/list")]
        public async Task<IEnumerable<PluginInfo>> List()
        {
            return this._pluginInfoDbContext.PluginInfoDbSet;
        }

        // POST: PluginsController/Create
        [HttpPost]
        public async Task<ActionResult> Upload(IFormCollection files)
        {
            try
            {
                await this._asyncFtpClient.AutoConnect();
                foreach (var file in files)
                {
                    
                }
                return null;
            }
            catch (Exception ex)
            {
                return new JsonResult(new
                {
                    ErrorCode = ex.HResult,
                    Message = ex.Message
                });
            }
        }

        // POST: PluginsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string pluginName, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

    }
}
