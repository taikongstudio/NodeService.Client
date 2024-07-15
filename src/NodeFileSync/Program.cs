using Microsoft.Extensions.FileSystemGlobbing;
using NodeService.Infrastructure;
using NodeService.Infrastructure.NodeFileSystem;

namespace NodeFileSync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Matcher matcher = new();
            var files = Directory.GetFiles("D:\\TestDirectory", "*", new EnumerationOptions()
            {
                RecurseSubdirectories = true
            });
            foreach (var item in files)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine();
            //matcher.AddIncludePatterns(new[] { "**/btv/**" });
            matcher.AddIncludePatterns(new[] { "**/btv/**/*.db" });
            matcher.AddExcludePatterns(new[] { "**/btv/bak/**" });
            matcher.AddExcludePatterns(new[] { "**/btv/*s.db" });
            //matcher.AddExcludePatterns(new[] { "**/btv/**" });
            //matcher.AddExcludePatterns(new[] { "*s.db" });

            var files1 = matcher.GetResultsInFullPath("D:\\TestDirectory");
            foreach (var item in files1)
            {
                Console.WriteLine(item);
            }
            Console.WriteLine();
           // Console.ReadLine();

            string searchDirectory = "../starting-folder/";

            IEnumerable<string> matchingFiles = matcher.GetResultsInFullPath(searchDirectory);


            ApiService apiService = new ApiService(new HttpClient()
            {
                BaseAddress = new Uri("http://localhost:5000"),
                Timeout = TimeSpan.FromHours(1)
            });
            try
            {

                var fileInfo = new FileInfo("D:\\Downloads\\Evernote_7.2.2.8065.exe");
                string compressedFilePath = null;
                FileStream compressedStream = fileInfo.OpenRead();
                var req =  NodeFileSyncRequestBuilder.FromFileInfo(
                    fileInfo,
                                        "DebugMachine",
                    Guid.NewGuid().ToString(),
                    "195eeae5-c0ac-480d-a126-75b1bc2f8f36",
                    NodeFileSyncConfigurationProtocol.Ftp,
                    $"/debugtest/{fileInfo.Name}");
                compressedStream?.Seek(0, SeekOrigin.Begin);
                var uploadRsp = await apiService.UploadFileAsync(req, compressedStream ?? File.OpenRead(fileInfo.FullName));
                File.Delete(compressedFilePath);


            }
            catch (Exception ex)
            {

            }

        }
    }
}
