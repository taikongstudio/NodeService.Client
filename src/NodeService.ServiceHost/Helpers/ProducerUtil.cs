using Confluent.Kafka;

namespace NodeService.ServiceHost.Helpers
{
    public class ProducerUtil
    {
        ILogger _logger;
        ProducerConfig kafkaProducerConfig = null;

        private KafkaConfigModel _kafkaConfig;

        public ProducerUtil(ILogger logger)
        {
            _logger = logger;
        }

        public void UpdateConfig(KafkaConfigModel kafkaConfig)
        {
            _kafkaConfig = kafkaConfig;
            kafkaProducerConfig = new ProducerConfig
            {
                BootstrapServers = kafkaConfig.BrokerList
            };
        }

        public async Task<bool> SendAsync(string msg)
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
                using (var p = new ProducerBuilder<Null, string>(kafkaProducerConfig).Build())
                {
                    var result = await p.ProduceAsync(_kafkaConfig.Topics.FirstOrDefault(x => x.Name == "shucaishouhu").Value, new Message<Null, string> { Value = msg });

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
