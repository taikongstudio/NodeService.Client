
using CommandLine;
using System.Text.Json;

namespace NodeService.DaemonService
{
    public class Options
    {


        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
