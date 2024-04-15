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

        private readonly ConcurrentDictionary<string, LinkedList<RentedArray<string>>> _timeDataDictionary;



        public MaccorDataReaderWriter(
            ILogger<MaccorDataReaderWriter> logger
            )
        {
            _logger = logger;
            _timeDataDictionary = new ConcurrentDictionary<string, LinkedList<RentedArray<string>>>();
        }



        public bool WriteTimeDataArray(string key, params TimeData[] timeDataArray)
        {
            if (!timeDataArray.Any())
            {
                return true;
            }
            try
            {
                var itemsToWrite = timeDataArray.Where(static x => x.HasValue);
                var totalCount = itemsToWrite.Count();
                var writtenCount = 0;
                Stack<TimeData> stack = new Stack<TimeData>(PageSize);
                do
                {
                    for (int i = 0; i < totalCount - writtenCount; i++)
                    {
                        if (i == PageSize)
                        {
                            break;
                        }
                        stack.Push(timeDataArray[i]);
                    }
   
                    var array = ArrayPool<string>.Shared.Rent(PageSize);
                    if (!_timeDataDictionary.TryGetValue(key, out var linkList))
                    {
                        linkList = new LinkedList<RentedArray<string>>();
                        _timeDataDictionary.TryAdd(key, linkList);
                    }
                    linkList.AddLast(new LinkedListNode<RentedArray<string>>(new RentedArray<string>(array)));
                    int index = 0;
                    while (stack.Count > 0)
                    {
                        var entry = stack.Pop();
                        var value = JsonSerializer.Serialize(entry);
                        array[index] = value;
                        index++;
                        writtenCount++;
                    }
                } while (writtenCount < totalCount);

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

        public RentedArray<string> ReadTimeData(string fileName,
            int pageIndex,
            CancellationToken cancellationToken = default)
        {
            if (!this._timeDataDictionary.TryGetValue(fileName, out var linkedList))
            {
                return RentedArray<string>.Empty;
            }
            var rentedObject = linkedList.ElementAtOrDefault(pageIndex);
            if (!rentedObject.HasValue)
            {
                return RentedArray<string>.Empty;
            }
            return rentedObject;
        }

    }
}


