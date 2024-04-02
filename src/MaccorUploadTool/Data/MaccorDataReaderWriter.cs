using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RocksDbSharp;
using MaccorUploadTool.Models;
using MaccorUploadTool.Helper;
using System.Reflection;

namespace MaccorUploadTool.Data
{
    public class MaccorDataReaderWriter
    {
        readonly BlockBasedTableOptions _bbto;
        readonly DbOptions _options;
        readonly ColumnFamilies _columnFamilies;
        readonly FlushOptions _flushOptions;
        readonly ILogger<MaccorDataReaderWriter> _logger;
        string dataColumnName = "data";
        readonly RocksDb _rocksDb;
        public string DatabasePath { get; private set; }



        public MaccorDataReaderWriter(
            ILogger<MaccorDataReaderWriter> logger
            )
        {
            _logger = logger;

            var logDbDirectory = Path.Combine(AppContext.BaseDirectory, "../logdb");
            this.DatabasePath = logDbDirectory;

            _bbto = new BlockBasedTableOptions()
            .SetFilterPolicy(BloomFilterPolicy.Create(10, false))
            .SetWholeKeyFiltering(false);

            _options = new DbOptions()
                .SetCreateIfMissing(true)
                .SetCreateMissingColumnFamilies(true);

            _columnFamilies = new ColumnFamilies
                {
                    { "default", new ColumnFamilyOptions().OptimizeForPointLookup(256) },
                    { dataColumnName, new ColumnFamilyOptions()
                        //.SetWriteBufferSize(writeBufferSize)
                        //.SetMaxWriteBufferNumber(maxWriteBufferNumber)
                        //.SetMinWriteBufferNumberToMerge(minWriteBufferNumberToMerge)
                        .SetMemtableHugePageSize(2 * 1024 * 1024)
                        .SetPrefixExtractor(SliceTransform.CreateFixedPrefix((ulong)50))
                        .SetBlockBasedTableFactory(_bbto)
                    },
                };
            _flushOptions = new FlushOptions();
            _flushOptions.SetWaitForFlush(true);

            _rocksDb = RocksDb.Open(_options, this.DatabasePath, _columnFamilies);
        }



        public bool WriteTimeDataArray(string fileName, params TimeData[] timeDataArray)
        {
            if (!timeDataArray.Any())
            {
                return true;
            }
            try
            {
                string id = MD5Helper.CalculateStringMD5(fileName);
                var cf = _rocksDb.GetColumnFamily(dataColumnName);
                if (!int.TryParse(_rocksDb.Get(id), out var index))
                {
                    index = 0;
                }
                var itemsToWrite = timeDataArray.Where(static x => x.HasValue);
                var totalCount = itemsToWrite.Count();
                var writeCount = 0;
                Stack<TimeData> stack = new Stack<TimeData>(256);

                do
                {
                    for (int i = 0; i < totalCount - writeCount; i++)
                    {
                        if (i == 1024)
                        {
                            break;
                        }
                        stack.Push(timeDataArray[i]);
                    }
                    WriteBatch writeBatch = new WriteBatch();
                    while (stack.Count > 0)
                    {
                        var entry = stack.Pop();
                        var key = GetKey(id, index);
                        var value = JsonSerializer.Serialize(entry);
                        writeBatch.Put(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value), cf: cf);
                        index++;
                        writeCount++;
                    }
                    _rocksDb.Write(writeBatch);
                } while (writeCount < totalCount);




                _rocksDb.Put(id, index.ToString());
                _rocksDb.Flush(_flushOptions);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return false;
        }

        public void Delete(string fileName)
        {
            string id = MD5Helper.CalculateStringMD5(fileName);
            var count = GetTimeDataCount(fileName);
            for (int index = 0; index < count; index++)
            {
                var key = GetKey(id, index);
                _rocksDb.Remove(key);
            }
            _rocksDb.Put(id, 0.ToString());
            _rocksDb.Flush(_flushOptions);
            this._logger.LogInformation($"{fileName}:Delete {count} items");
        }

        private string GetKey(string id, int index)
        {
            return $"{id}_Data_{index}";
        }

        public int GetTimeDataCount(string fileName)
        {
            string id = MD5Helper.CalculateStringMD5(fileName);
            var cf = _rocksDb.GetColumnFamily(dataColumnName);
            var value = _rocksDb.Get(id);
            if (!int.TryParse(value, out var index))
            {
                index = 0;
            }
            return index;
        }

        public IEnumerable<string> ReadTimeData(string fileName,
            int pageIndex,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            string id = MD5Helper.CalculateStringMD5(fileName);
            var readOptions = new ReadOptions();
            var cf = _rocksDb.GetColumnFamily(dataColumnName);
            var logLength = GetTimeDataCount(fileName);
            for (int index = pageSize * pageIndex; index < logLength && index < (pageIndex + 1) * pageSize; index++)
            {
                var key = GetKey(id, index);
                var value = _rocksDb.Get(key, cf, encoding: Encoding.UTF8);
                yield return value;
            }
            yield break;
        }

        public void Dispose()
        {
            _rocksDb.Dispose();
        }
    }
}


