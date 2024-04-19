using Microsoft.Extensions.Logging;
using NodeService.Infrastructure.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MaccorUploadTool.Models;
using MaccorUploadTool.Helper;
using System.Reflection;
using System.Collections.Concurrent;
using System.Buffers;

namespace MaccorUploadTool.Data
{
    public class MaccorDataReaderWriter
    {
        public const int PageSize = 1024;

        readonly ILogger<MaccorDataReaderWriter> _logger;

        public string DatabasePath { get; private set; }

        private readonly ConcurrentDictionary<string, LinkedList<RentedArray<(int, string)>>> _timeDataDictionary;



        public MaccorDataReaderWriter(
            ILogger<MaccorDataReaderWriter> logger
            )
        {
            _logger = logger;
            _timeDataDictionary = new ConcurrentDictionary<string, LinkedList<RentedArray<(int, string)>>>();
        }


        public void Verify(string key)
        {
            if (!this._timeDataDictionary.TryGetValue(key, out var linkedList))
            {
                return;
            }
            int index = -1;
            foreach (var array in linkedList)
            {
                if (!array.HasValue)
                {
                    throw new InvalidOperationException();
                }

                foreach (var data in array.Value)
                {
                    if (index != data.Item1 - 1)
                    {
                        throw new InvalidOperationException();
                    }
                    index = data.Item1;
                }
            }
        }


        public bool WriteTimeDataArray(string key, RentedArray<TimeData> timeDataArray)
        {
            if (!timeDataArray.HasValue)
            {
                return true;
            }
            try
            {
                var itemsToWrite = timeDataArray.Value.Where(static x => x.Index >= 0);
                var totalCount = itemsToWrite.Count();
                var writtenCount = 0;
                int pageCount = Math.DivRem(totalCount, MaccorDataReaderWriter.PageSize, out var result);
                var linkList = _timeDataDictionary.GetOrAdd(key, new LinkedList<RentedArray<(int,string)>>());
                for (int i = 0; i < pageCount; i++)
                {
                    int index = 0;
                    var rentedArray = new RentedArray<(int, string)>(PageSize);
                    linkList.AddLast(new LinkedListNode<RentedArray<(int, string)>>(rentedArray));
                    foreach (var item in itemsToWrite.Skip(i * PageSize).Take(PageSize))
                    {
                        var value = JsonSerializer.Serialize(item);
                        rentedArray.Value[index] = (item.Index, value);
                        index++;
                        writtenCount++;
                    }
                }
                _logger.LogInformation($"Write {writtenCount} total {totalCount}");
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
            if (!this._timeDataDictionary.TryRemove(fileName, out var linkedList))
            {
                return;
            }
            var current = linkedList.First;
            while (current != null)
            {
                current.ValueRef.Dispose();
                current = current.Next;
            }
            linkedList.Clear();
        }

        public RentedArray<(int,string)> ReadTimeData(string fileName,
            int pageIndex,
            CancellationToken cancellationToken = default)
        {
            if (!this._timeDataDictionary.TryGetValue(fileName, out var linkedList))
            {
                return RentedArray<(int, string)>.Empty;
            }
            var rentedObject = linkedList.ElementAtOrDefault(pageIndex);
            if (!rentedObject.HasValue)
            {
                return RentedArray<(int, string)>.Empty;
            }
            return rentedObject;
        }

    }
}


