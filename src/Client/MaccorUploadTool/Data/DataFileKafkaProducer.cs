using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace MaccorUploadTool.Data
{
    public class DataFileKafkaProducer : IDisposable
    {
        private bool disposedValue;
        private ILogger _logger;

        public DataFileKafkaProducer(ILogger logger, string brokerList, string headerTopicName, string timeDataTopicName)
        {
            _logger = logger;
            BrokerList = brokerList;
            HeaderTopicName = headerTopicName;
            TimeDataTopicName = timeDataTopicName;
            var config = new ProducerConfig { BootstrapServers = BrokerList, Acks = Acks.Leader };
            config.SocketTimeoutMs = 60000;
            Producer = new ProducerBuilder<string, string>(config).Build();
        }

        public string BrokerList { get; set; }

        public string TimeDataTopicName { get; set; }

        public string HeaderTopicName { get; set; }



        public IProducer<string, string> Producer { get; private set; }


        public void BeginTransaction()
        {
            Producer.BeginTransaction();
        }

        public void InitTransactions(TimeSpan timeout)
        {
            Producer.InitTransactions(timeout);
        }

        public void CommitTransaction()
        {
            Producer.CommitTransaction();
        }

        public void AbortTransaction()
        {
            Producer.AbortTransaction();
        }


        public async Task<bool> ProduceHeaderAsync(string key, string value)
        {
            //Console.WriteLine($"Producer {this.Producer.Name} producing on topic {TopicName}.");

            try
            {
                // Note: Awaiting the asynchronous produce request below prevents flow of execution
                // from proceeding until the acknowledgement from the broker is received (at the 
                // expense of low throughput).
                var deliveryReport = await Producer.ProduceAsync(
                    HeaderTopicName, new Message<string, string> { Key = key, Value = value });

                //this._logger.LogInformation($"{deliveryReport.TopicPartitionOffset}");
                return true;
            }
            catch (ProduceException<string, string> e)
            {
                _logger.LogError($"failed to deliver message: {e.Message} [{e.Error.Code}]: {value}");
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
            return false;

            // Since we are producing synchronously, at this point there will be no messages
            // in-flight and no delivery reports waiting to be acknowledged, so there is no
            // need to call producer.Flush before disposing the producer.
        }

        public async Task<bool> ProduceTimeDataAsync(string key, string value)
        {
            //Console.WriteLine($"Producer {this.Producer.Name} producing on topic {TopicName}.");

            try
            {
                // Note: Awaiting the asynchronous produce request below prevents flow of execution
                // from proceeding until the acknowledgement from the broker is received (at the 
                // expense of low throughput).
                var deliveryReport = await Producer.ProduceAsync(
                    TimeDataTopicName, new Message<string, string> { Key = key, Value = value });

                //this._logger.LogInformation($"{deliveryReport.TopicPartitionOffset}");
                return true;
            }
            catch (ProduceException<string, string> e)
            {
                _logger.LogError($"failed to deliver message: {e.Message} [{e.Error.Code}]: {value}");
                //Console.WriteLine($"failed to deliver message: {e.Message} [{e.Error.Code}]");
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
            return false;
            // Since we are producing synchronously, at this point there will be no messages
            // in-flight and no delivery reports waiting to be acknowledged, so there is no
            // need to call producer.Flush before disposing the producer.
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Producer.Dispose();
                }

                // TODO: 释放未托管的资源(未托管的对象)并重写终结器
                // TODO: 将大型字段设置为 null
                disposedValue = true;
            }
        }

        // // TODO: 仅当“Dispose(bool disposing)”拥有用于释放未托管资源的代码时才替代终结器
        // ~DataFileKafkaProducer()
        // {
        //     // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 不要更改此代码。请将清理代码放入“Dispose(bool disposing)”方法中
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public void Flush()
        {
            Producer.Flush();
        }
    }
}
