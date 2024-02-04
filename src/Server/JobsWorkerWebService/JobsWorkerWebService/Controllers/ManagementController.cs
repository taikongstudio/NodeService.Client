using JobsWorker.Shared.GrpcModels;
using JobsWorker.Shared.Models;
using JobsWorkerWebService.Data;
using Microsoft.AspNetCore.Mvc;
using OneOf.Types;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ManagementController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly ApplicationProfileDbContext _applicationProfileDbContext;
        public ManagementController(
            ApplicationDbContext applicationDbContext,
            ApplicationProfileDbContext applicationProfileDbContext
            )
        {
            this._applicationDbContext = applicationDbContext;
            this._applicationProfileDbContext = applicationProfileDbContext;
        }

        [HttpGet("/api/management/sync")]
        public async Task<ApiResult<int>> SyncNodeInfoFromMachineInfoAsync()
        {
            ApiResult<int> apiResult = new ApiResult<int>();
            var machineInfoList = this._applicationProfileDbContext
                     .MachineInfoDbSet.ToArray();

            var nodesList = this._applicationDbContext.NodeInfoDbSet.ToList();

            foreach (var machineInfo in machineInfoList)
            {
                string computerName = machineInfo.computer_name;
                var nodeInfo = nodesList.FirstOrDefault(
                    x =>
                    string.Equals(x.node_name, computerName, StringComparison.OrdinalIgnoreCase)
                    );


                if (nodeInfo == null)
                {
                    nodeInfo = NodeInfo.Create(machineInfo.computer_name);
                    this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
                    nodesList.Add(nodeInfo);
                }
                nodeInfo.test_info = machineInfo.test_info;
                nodeInfo.remarks = machineInfo.remarks;
                nodeInfo.lab_name = machineInfo.lab_name;
                nodeInfo.usages = machineInfo.usages;
                nodeInfo.lab_area = machineInfo.lab_area;
            }

            var changesCount = await this._applicationDbContext.SaveChangesAsync();
            apiResult.Value = changesCount;
            return apiResult;
        }

    }
}
