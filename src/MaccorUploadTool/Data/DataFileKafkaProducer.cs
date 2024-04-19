using Confluent.Kafka;
using Google.Protobuf.WellKnownTypes;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Channels;

namespace MaccorUploadTool.Data
{
    public class DataFileKafkaProducer : IDisposable
    {
        private bool disposedValue;
        private readonly FileSystemChangedRecord _fileSystemChangedRecord;
        private ILogger _logger;
        private ProducerConfig _producerConfig;
        private Channel<Message<string, string>> _headerChanel;
        private Channel<string> _timeDataChannel;

        public DataFileKafkaProducer(
            ILogger logger,
            FileSystemChangedRecord fileSystemChangedRecord,
            string brokerList,
            string headerTopicName,
            string timeDataTopicName)
        {
            _headerChanel = Channel.CreateUnbounded<Message<string, string>>();
            _timeDataChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
            _fileSystemChangedRecord = fileSystemChangedRecord;
            _logger = logger;
            BrokerList = brokerList;
            HeaderTopicName = headerTopicName;
            TimeDataTopicName = timeDataTopicName;
            _producerConfig = new ProducerConfig
            {
                BootstrapServers = BrokerList,
                Acks = Acks.All,
                SocketTimeoutMs = 60000,
                LingerMs = 20,
                BatchNumMessages = 10000
            };
            Producer = new ProducerBuilder<string, string>(_producerConfig).Build();
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


        public async Task<bool> ProduceHeaderAsync(CancellationToken cancellationToken = default)
        {
            try
            {

                foreach (var header in _fileSystemChangedRecord.DataFile.DataFileHeader)
                {
                    await this._headerChanel.Writer.WriteAsync(new Message<string, string> { Key = null, Value = header.AsJsonString() });

                }
                int index = 0;
                await foreach (var message in this._headerChanel.Reader.ReadAllAsync())
                {
                    index++;
                    Producer.Produce(HeaderTopicName, message, HeaderDeliveryHandler);
                    if (index == 1024)
                    {
                        index = 0;
                        Producer.Flush();
                    }
                }
                Producer.Flush();
                //_logger.LogInformation($"{deliveryReport.TopicPartitionOffset}");
                return true;
            }
            catch (ProduceException<string, string> e)
            {
                _logger.LogError(e.ToString());
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


        private void HeaderDeliveryHandler(DeliveryReport<string, string> deliveryReport)
        {
            if (deliveryReport.Error.Code == ErrorCode.NoError)
            {
                this._fileSystemChangedRecord.Stat.HeaderDataUploadCount++;
                if (this._fileSystemChangedRecord.Stat.HeaderDataCount == this._fileSystemChangedRecord.Stat.HeaderDataUploadCount)
                {
                    this._headerChanel.Writer.Complete();
                }
                return;
            }
            this._fileSystemChangedRecord.Stat.HeaderDataTotalRetryTimes++;
            this._headerChanel.Writer.TryWrite(deliveryReport.Message);
        }

        private void TimeDataDeliveryHandler(DeliveryReport<string, string> deliveryReport)
        {
            if (deliveryReport.Error.Code == ErrorCode.NoError)
            {
                this._fileSystemChangedRecord.Stat.IncrementTimeDataUploadCount();
                //_logger.LogInformation($"{this._fileSystemChangedRecord.Stat.TimeDataUploadCount}");
                return;
            }
            _logger.LogInformation($"retry:{deliveryReport.Value}");
            this._fileSystemChangedRecord.Stat.TimeDataTotalRetryTimes++;
            this._timeDataChannel.Writer.TryWrite(deliveryReport.Value);
            this._timeDataChannel.Writer.TryWrite(null);
        }

        public async Task<bool> ProduceTimeDataAsync()
        {
            try
            {
                _ = Task.Run(async () =>
                {

                    int index = 0;
                    for (int pageIndex = 0; pageIndex < this._fileSystemChangedRecord.DataFile.TimeDataLinkedList.Count; pageIndex++)
                    {
                        var timeDataArray = this._fileSystemChangedRecord.DataFile.ReadTimeData(pageIndex);

                        if (timeDataArray == null)
                        {
                            break;
                        }
                        int count = 0;
                        foreach (var timeData in timeDataArray)
                        {
                            if (timeData == null)
                            {
                                continue;
                            }
                            index++;
                            await this._timeDataChannel.Writer.WriteAsync(timeData.Json);
                            count++;
                        }
                        if (count > 0)
                        {
                            _logger.LogInformation($"{this._fileSystemChangedRecord.LocalFilePath}:Write {count} items, total write {index}/{this._fileSystemChangedRecord.Stat.TimeDataCount} items,sent {this._fileSystemChangedRecord.Stat.TimeDataUploadCount}/{this._fileSystemChangedRecord.Stat.TimeDataCount}");
                        }
                        while (this._timeDataChannel.Reader.CanCount && this._timeDataChannel.Reader.Count > DataFile.PageSize * 2)
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }
                    await this._timeDataChannel.Writer.WriteAsync(null);
                    if (index == this._fileSystemChangedRecord.Stat.TimeDataCount)
                    {
                        _logger.LogInformation($"{this._fileSystemChangedRecord.LocalFilePath}:Write completed,Write {index} items,sent:{this._fileSystemChangedRecord.Stat.TimeDataUploadCount} items");
                    }
                    int waitCount = 0;
                    while (this._fileSystemChangedRecord.Stat.TimeDataUploadCount < this._fileSystemChangedRecord.Stat.TimeDataCount)
                    {
                        if (this._fileSystemChangedRecord.Stat.TimeDataCount == this._fileSystemChangedRecord.Stat.TimeDataUploadCount)
                        {
                            break;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        waitCount++;
                        if (waitCount % 20 == 0)
                        {
                            await this._timeDataChannel.Writer.WriteAsync(null);
                            _logger.LogInformation($"Wait for upload completed:Total {this._fileSystemChangedRecord.Stat.TimeDataCount} Uploaded:{this._fileSystemChangedRecord.Stat.TimeDataUploadCount}");
                        }
                    }
                    _logger.LogInformation($"Uploaded:{this._fileSystemChangedRecord.Stat.TimeDataUploadCount} Total:{this._fileSystemChangedRecord.Stat.TimeDataCount}");
                    this._timeDataChannel.Writer.Complete();

                });



                await SendTimeDataAsync();

                //_logger.LogInformation($"{deliveryReport.TopicPartitionOffset}");
                return true;
            }
            catch (ProduceException<string, string> e)
            {
                _logger.LogError(e.ToString());
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

        private async Task SendTimeDataAsync()
        {
            try
            {
                int count = 0;
                await foreach (var message in this._timeDataChannel.Reader.ReadAllAsync())
                {
                    if (message == null)
                    {
                        _logger.LogInformation("Flush");
                        Producer.Flush();
                        continue;
                    }
                    Producer.Produce(TimeDataTopicName, new Message<string, string>() { Key = null, Value = message }, TimeDataDeliveryHandler);
                    count++;
                    if (count == DataFile.PageSize)
                    {
                        count = 0;
                        Producer.Flush();
                        _logger.LogInformation("Flush");
                    }
                }
                Producer.Flush();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
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
    }
}
