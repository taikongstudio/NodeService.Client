using Confluent.Kafka;
using Google.Protobuf.WellKnownTypes;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace MaccorUploadTool.Data
{
    public class DataFileKafkaProducer : IDisposable
    {
        private bool disposedValue;
        private readonly DataFileContext context;
        private ILogger _logger;
        private ProducerConfig _producerConfig;
        private Channel<Message<string, string>> _headerChanel;
        private Channel<string> _timeDataChannel;

        public DataFileKafkaProducer(
            ILogger logger,
            DataFileContext fileSystemChangedRecord,
            string brokerList,
            string headerTopicName,
            string timeDataTopicName)
        {
            _headerChanel = Channel.CreateUnbounded<Message<string, string>>();
            _timeDataChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(1024 * 4)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
            context = fileSystemChangedRecord;
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

                foreach (var header in context.DataFile.DataFileHeader)
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
                this.context.Stat.HeaderDataUploadCount++;
                if (this.context.Stat.HeaderDataCount == this.context.Stat.HeaderDataUploadCount)
                {
                    this._headerChanel.Writer.Complete();
                }
                return;
            }
            this.context.Stat.HeaderDataTotalRetryTimes++;
            this._headerChanel.Writer.TryWrite(deliveryReport.Message);
        }

        private void TimeDataDeliveryHandler(DeliveryReport<string, string> deliveryReport)
        {
            if (deliveryReport.Error.Code == ErrorCode.NoError)
            {
                this.context.Stat.IncrementTimeDataUploadCount();
                //_logger.LogInformation($"{this._fileSystemChangedRecord.Stat.TimeDataUploadCount}");
                return;
            }
            _logger.LogInformation($"retry:{deliveryReport.Value}");
            this.context.Stat.TimeDataTotalRetryTimes++;
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
                    int count = 0;
                    foreach (var timeData in this.context.DataFile.TimeDatas)
                    {
                        if (timeData == null)
                        {
                            continue;
                        }

                        timeData.Json = JsonSerializer.Serialize(timeData);

                        index++;
                        count++;
                        await this._timeDataChannel.Writer.WriteAsync(timeData.Json);

                        if (count >= DataFile.PageSize)
                        {
                            _logger.LogInformation($"{this.context.LocalFilePath}:read {index} items, read {index}/{this.context.DataFileReader.TimeDataCount} items,sent {this.context.Stat.TimeDataUploadCount}");
                            count = 0;
                        }
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    _logger.LogInformation($"{this.context.LocalFilePath}:read {index} items, read {index}/{this.context.DataFileReader.TimeDataCount} items,sent {this.context.Stat.TimeDataUploadCount}");


                    await this._timeDataChannel.Writer.WriteAsync(null);

                    this.context.Stat.TimeDataCount = this.context.DataFileReader.TimeDataCount;

                    if (index == this.context.Stat.TimeDataCount)
                    {
                        _logger.LogInformation($"{this.context.LocalFilePath}:Write completed,Write {index} items,sent:{this.context.Stat.TimeDataUploadCount} items");
                    }
                    int waitCount = 0;
                    while (this.context.Stat.TimeDataUploadCount < this.context.Stat.TimeDataCount)
                    {
                        if (this.context.Stat.TimeDataCount == this.context.Stat.TimeDataUploadCount)
                        {
                            break;
                        }
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        waitCount++;
                        if (waitCount % 20 == 0)
                        {
                            await this._timeDataChannel.Writer.WriteAsync(null);
                            _logger.LogInformation($"Wait for upload completed:Total {this.context.Stat.TimeDataCount} Uploaded:{this.context.Stat.TimeDataUploadCount}");
                        }
                    }
                    _logger.LogInformation($"Uploaded:{this.context.Stat.TimeDataUploadCount} Total:{this.context.Stat.TimeDataCount}");
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
