using JobsWorker.Shared.DataModels;
using JobsWorker.Shared.MessageQueues.Models;
using JobsWorker.Shared.MessageQueues;
using Microsoft.AspNetCore.Mvc;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class JobsController : Controller
    {
        private readonly ILogger<NodesController> _logger;
        private readonly ApplicationDbContext _applicationDbContext;


        public JobsController(
            ApplicationDbContext applicationDbContext,
            ILogger<NodesController> logger)
        {
            this._logger = logger;
            this._applicationDbContext = applicationDbContext;
        }

        [HttpGet("/api/jobs/instances/list")]
        public async Task<ApiResult<IEnumerable<JobExecutionInstanceModel>>> QueryJobExecutionInstanceListAsync(
            [FromQuery] DateTime? startDateTime,
            [FromQuery] DateTime? endDateTime,
            [FromQuery] int? pageSize,
            [FromQuery] int? pageIndex
            )
        {
            ApiResult<IEnumerable<JobExecutionInstanceModel>> apiResult = new ApiResult<IEnumerable<JobExecutionInstanceModel>>();
            try
            {
                if (startDateTime == null)
                {
                    startDateTime = DateTime.Today.Date;
                }
                if (endDateTime == null)
                {
                    endDateTime = DateTime.Today.Date.AddDays(1).AddSeconds(-1);
                }
                if (pageSize == null)
                {
                    pageSize = 0;
                }
                if (pageIndex == null)
                {
                    pageIndex = 0;
                }
                var queryable = this._applicationDbContext.JobExecutionInstancesDbSet.AsQueryable();
                if (startDateTime != null)
                {
                    queryable = queryable.Where(x => x.FireTime.Date >= startDateTime && x.FireTime < endDateTime);
                }
                if (pageSize != 0)
                {
                    queryable = queryable.Skip(pageSize.Value * pageIndex.Value)
                    .Take(pageSize.Value);
                }
                apiResult.Result = await queryable.OrderByDescending(x => x.FireTime).ToArrayAsync();
            }
            catch (Exception ex)
            {
                apiResult.ErrorCode = ex.HResult;
                apiResult.Message = ex.Message;
            }
            return apiResult;
        }

    }
}
