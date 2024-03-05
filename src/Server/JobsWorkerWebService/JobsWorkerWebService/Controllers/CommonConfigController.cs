using JobsWorker.Shared.MessageQueues;

namespace JobsWorkerWebService.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public partial class CommonConfigController : Controller
    {
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly IMemoryCache _memoryCache;
        private readonly IVirtualFileSystem _virtualFileSystem;
        private readonly VirtualFileSystemConfig _virtualFileSystemConfig;
        private readonly ILogger<CommonConfigController> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CommonConfigController(
            IMemoryCache memoryCache,
            ApplicationDbContext applicationDbContext,
            ILogger<CommonConfigController> logger,
            IVirtualFileSystem virtualFileSystem,
            VirtualFileSystemConfig virtualFileSystemConfig,
            IServiceProvider serviceProvider)
        {
            this._serviceProvider = serviceProvider;
            this._applicationDbContext = applicationDbContext;
            this._memoryCache = memoryCache;
            this._virtualFileSystem = virtualFileSystem;
            this._virtualFileSystemConfig = virtualFileSystemConfig;
            this._logger = logger;
        }
    }
}
