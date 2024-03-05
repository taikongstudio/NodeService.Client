using JobsWorker.Shared.DataModels;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class ManagementController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IServiceProvider  _serviceProvider;
        public ManagementController(
            IServiceProvider serviceProvider,
            ApplicationDbContext applicationDbContext
            )
        {
            this._serviceProvider = serviceProvider;
            this._applicationDbContext = applicationDbContext;
        }

        [HttpGet("/api/management/sync")]
        public async Task<ApiResult<int>> SyncNodeInfoFromMachineInfoAsync()
        {
            ApiResult<int> apiResult = new ApiResult<int>();
            using var scope = this._serviceProvider.CreateScope();
            using var profileDbContext = this._serviceProvider.GetService<ApplicationProfileDbContext>();
            var machineInfoList = profileDbContext.MachineInfoDbSet.ToArray();

            var nodesList = this._applicationDbContext.NodeInfoDbSet.ToList();

            foreach (var machineInfo in machineInfoList)
            {
                string computerName = machineInfo.computer_name;
                var nodeInfo = nodesList.FirstOrDefault(
                    x =>
                    string.Equals(x.Name, computerName, StringComparison.OrdinalIgnoreCase)
                    );


                if (nodeInfo == null)
                {
                    nodeInfo = NodeInfoModel.Create(machineInfo.computer_name);
                    this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
                    nodesList.Add(nodeInfo);
                }
                nodeInfo.Profile.TestInfo = machineInfo.test_info;
                nodeInfo.Profile.Remarks = machineInfo.remarks;
                nodeInfo.Profile.LabName = machineInfo.lab_name;
                nodeInfo.Profile.Usages = machineInfo.usages;
                nodeInfo.Profile.LabArea = machineInfo.lab_area;
            }

            var changesCount = await this._applicationDbContext.SaveChangesAsync();
            apiResult.Result = changesCount;
            return apiResult;
        }

        [HttpGet("/api/management/test")]
        public async Task<ApiResult<int>> TestAsync()
        {
            ApiResult<int> apiResult = new ApiResult<int>();

            for (int i = 0; i < 1000; i++)
            {



                var nodeInfo = NodeInfoModel.Create(Guid.NewGuid().ToString());
                this._applicationDbContext.NodeInfoDbSet.Add(nodeInfo);
            }

            var changesCount = await this._applicationDbContext.SaveChangesAsync();
            apiResult.Result = changesCount;
            return apiResult;
        }

    }
}
