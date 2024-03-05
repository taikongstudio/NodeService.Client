using JobsWorker.Shared.DataModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Models
{
    public static class JobScheduleConfigExtensions
    {
        public static T? GetOptions<T>(this JobScheduleConfigModel jobScheduleConfig)
        {
            try
            {
                return jobScheduleConfig.optionsElement.Deserialize<T>();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return default(T?);
        }

    }
}
