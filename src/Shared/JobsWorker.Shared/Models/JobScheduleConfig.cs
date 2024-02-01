using System.Text.Json;

namespace JobsWorker.Shared.Models
{
    public class JobScheduleConfig
    {
        public string jobName { get; set; }
        public bool isEnabled { get; set; }

        public string jobId { get; set; }

        public string jobType { get; set; }

        public string[] cronExpressions { get; set; }

        public IDictionary<string, object> options { get; set; }

        public string[] dnsNameFilters { get; set; }

        public string dnsNameFilterType { get; set; }

        public string[] IpAddressesFilters { get; set; }

        public string IpAddressesFilterType { get; set; }

        public bool executeNow { get; set; }

        public string factoryName { get; set; }


        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

    }
}
