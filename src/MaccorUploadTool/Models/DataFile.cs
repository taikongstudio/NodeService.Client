using MaccorUploadTool.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MaccorUploadTool.Models
{
    public class DataFile
    {
        public const int PageSize = 1024;

        public List<DataFileHeader> DataFileHeader { get; set; } = [];

        public LinkedList<TimeData[]> TimeDataLinkedList { get; set; } = [];

        public void VerifyTimeData()
        {
            int index = -1;
            foreach (var array in TimeDataLinkedList)
            {
                foreach (var data in array)
                {
                    if (data == null)
                    {
                        continue;
                    }
                    if (index != data.Index - 1)
                    {
                        throw new InvalidOperationException();
                    }
                    index = data.Index;
                }
            }
        }


        public void WriteTimeData(List<TimeData> timeDataArray)
        {
            var writtenCount = 0;
            int pageCount = Math.DivRem(timeDataArray.Count, PageSize, out var result);
            if (result > 0)
            {
                pageCount += 1;
            }
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                int index = 0;
                var node = new LinkedListNode<TimeData[]>(new TimeData[PageSize]);
                TimeDataLinkedList.AddLast(node);
                foreach (var item in timeDataArray.Skip(PageSize * pageIndex).Take(PageSize))
                {

                    var value = JsonSerializer.Serialize(item);
                    item.Json = value;
                    node.ValueRef[index] = item;
                    index++;
                    writtenCount++;
                }
            }
            if (writtenCount != timeDataArray.Count)
            {
                throw new InvalidDataException();
            }
        }

        public TimeData[]? ReadTimeData(int pageIndex)
        {
            return TimeDataLinkedList.ElementAtOrDefault(pageIndex);
        }
    }
}
