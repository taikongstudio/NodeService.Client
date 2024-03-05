namespace JobsWorkerWebService.Controllers
{
    public partial class NodesController
    {

        [HttpGet("/api/nodes/{id}/filesystem/{**path}")]
        public async Task<ApiResult<IEnumerable<FileSystemEntry>>> ListDirectoryAsync(string id, string path, [FromQuery] string? searchpattern)
        {
            ApiResult<IEnumerable<FileSystemEntry>> apiResult = new ApiResult<IEnumerable<FileSystemEntry>>();
            try
            {
                var nodeInfo = await this._applicationDbContext.NodeInfoDbSet.FindAsync(id);
                if (nodeInfo == null)
                {
                    apiResult.ErrorCode = -1;
                    apiResult.Message = "invalid node id";
                }
                else
                {
                    string requestId = Guid.NewGuid().ToString();
                    FileSystemListDirectoryRequest fileSystemListRequest = new FileSystemListDirectoryRequest()
                    {

                        Key = requestId,
                        Content = new FileSystemListDirectoryReq()
                        {
                            NodeName = nodeInfo.Name,
                            IncludeSubDirectories = false,
                            Directory = path,
                            RequestId = requestId,
                            SearchPattern = searchpattern,
                            Timeout = 60000
                        },
                        Timeout = TimeSpan.FromMicroseconds(60000),
                        DateTime = DateTime.Now,
                    };
                    FileSystemListDirectoryResponse? rsp =
                        await this._inprocRpc.SendAsync<FileSystemListDirectoryResponse>(id, fileSystemListRequest);
                    apiResult.ErrorCode = rsp.Content.ErrorCode;
                    apiResult.Message = rsp.Content.Message;
                    apiResult.Result = rsp.Content.FileSystemObjects.Select(x => new FileSystemEntry()
                    {
                        CreationTime = x.CreationTime.ToDateTime(),
                        FullName = x.FullName,
                        LastWriteTime = x.LastWriteTime.ToDateTime(),
                        Length = x.Length,
                        Name = x.Name,
                        Type = x.Type,
                    });
                }
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
