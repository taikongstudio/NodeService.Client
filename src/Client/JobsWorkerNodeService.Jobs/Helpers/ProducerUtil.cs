using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobsWorkerNodeService.Jobs.Helpers
{
    public class ProducerUtil
    {
        ILogger _logger;
        ProducerConfig kafka_config = null;

        public ProducerUtil(ILogger logger)
        {
            _logger = logger;
        }

        public void UpdateConfig(string kafkaBootstrapServers)
        {
            kafka_config = new ProducerConfig
            {
                BootstrapServers = kafkaBootstrapServers
            };
        }

        public bool SendMsg(string msg)
        {
            Action<DeliveryReport<Null, string>> handler = r =>
            {
                if (!r.Error.IsError)
                    _logger.LogInformation($"Delivered message to {r.TopicPartitionOffset}");
                else
                    _logger.LogError($"Delivery Error: {r.Error.Reason}");
            };
            try
            {
                using (var p = new ProducerBuilder<Null, string>(kafka_config).Build())
                {
                    p.Produce("shucaishouhu", new Message<Null, string> { Value = msg }, handler);

                    // wait for up to 10 seconds for any inflight messages to be delivered.
                    p.Flush(TimeSpan.FromSeconds(10));
                }
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
                return false;
            }
        }
    }
}
