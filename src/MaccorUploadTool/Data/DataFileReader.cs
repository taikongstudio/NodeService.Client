using MaccorUploadTool;
using MaccorUploadTool.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MaccorUploadTool.Data
{
    /// <summary>
    /// 数据读取器
    /// </summary>
    public class DataFileReader : IDisposable
    {
        //文件句柄
        private readonly int _handle;

        /// <summary>
        /// 使用有效的文件句柄初始化<see cref="DataFileReader"/>
        /// </summary>
        /// <param name="handle"></param>
        private DataFileReader(int handle)
        {
            _handle = handle;
        }


        /// <summary>
        /// 尝试从指定的文件路径初始化<see cref="DataFileReader"/>
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="dataReader">数据读取器</param>
        /// <returns>true:初始化成功;false:初始化失败。</returns>
        public static bool TryLoad(string filePath, ILogger logger, out Exception exception, out DataFileReader dataReader)
        {
            exception = null;
            dataReader = null;
            try
            {
                int handle = NativeMethods.OpenDataFile(filePath);
                if (handle >= 0)
                {

                    dataReader = new DataFileReader(handle);
                    exception = null;
                    return true;
                }
            }
            catch (SEHException sehEx)
            {
                exception = sehEx;
                return false;
            }
            catch (Exception ex)
            {
                exception = ex;
                logger?.LogError(ex.ToString());
            }
            return false;
        }


        /// <summary>
        /// 枚举<see cref="DataFileHeader"/>
        /// </summary>
        /// <returns><see cref="IEnumerable{T}" /></returns>
        public IEnumerable<DataFileHeader> ReadHeaders(ReadDataParameters parameters)
        {
            int handleTemp = _handle;
            int index = 0;
            DLLDataFileHeader dllDataFileHeader = new DLLDataFileHeader();
            Stopwatch stopwatch = Stopwatch.StartNew();
            handleTemp = NativeMethods.GetDataFileHeader(_handle, ref dllDataFileHeader);
            var dataFileHeader = new DataFileHeader();
            dataFileHeader.Init(_handle, dllDataFileHeader);
            yield return dataFileHeader;
            do
            {
                if (handleTemp != 0)
                {
                    break;
                }
                handleTemp = NativeMethods.LoadNextDataFileHeader(_handle);
                if (handleTemp != 0)
                {
                    break;
                }
                handleTemp = NativeMethods.GetDataFileHeader(handleTemp, ref dllDataFileHeader);
                if (handleTemp != 0)
                {
                    break;
                }
                dataFileHeader = new DataFileHeader();
                dataFileHeader.Init(_handle, dllDataFileHeader);
                dataFileHeader.DnsName = parameters.DnsName;
                dataFileHeader.FilePath = parameters.FilePath;
                dataFileHeader.IPAddress = parameters.IPAddress;
                yield return dataFileHeader;
            } while (handleTemp == 0);

            stopwatch.Stop();
            this.HeaderDataParseTime = stopwatch.Elapsed;
            yield break;
        }

        long _timeDataCount;

        public int TimeDataCount { get { return (int)Interlocked.Read(ref _timeDataCount); } }

        public TimeSpan TimeDataParseTime { get; private set; }
        public TimeSpan HeaderDataParseTime { get; private set; }

        private void IncrementTimeDataCount()
        {
            Interlocked.Increment(ref this._timeDataCount);
        }



        /// <summary>
        /// 枚举<see cref="TimeData"/>
        /// </summary>
        /// <returns></returns>
        public IEnumerable<TimeData> ReadTimeData(ReadDataParameters parameters)
        {
            DLLTimeData dllTimeData = default;
            int result = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            do
            {
                stopwatch.Start();
                result = NativeMethods.LoadAndGetNextTimeData(_handle, ref dllTimeData);
                if (result == 0)
                {
                    TimeData timeData = new TimeData();
                    timeData.DnsName = parameters.DnsName;
                    timeData.FilePath = parameters.FilePath;
                    timeData.IPAddress = parameters.IPAddress;
                    timeData.Init(dllTimeData);
                    timeData.SetIndex((int)TimeDataCount);
                    stopwatch.Stop();
                    IncrementTimeDataCount();
                    yield return timeData;
                }
                else
                {
                    break;
                }
            } while (true);
            stopwatch.Stop();
            this.TimeDataParseTime = stopwatch.Elapsed;
            yield break;
        }

        public unsafe ScopeTrace GetScopeTrace()
        {
            DLLScopeTrace dllScopeTrace = new DLLScopeTrace();
            int ret = NativeMethods.GetScopeTrace(_handle, ref dllScopeTrace);

            Sample[] samples = new Sample[dllScopeTrace.Samples];

            for (int i = 0; i < dllScopeTrace.Samples; i++)
            {
                Sample sample = new Sample();
                //array是以字节为单位的，所以这里乘以4，因为float是4个字节。
                sample.V = dllScopeTrace.Array[i * 4];
                sample.I = dllScopeTrace.Array[(i + 1) * 4];
                samples[i] = sample;
            }

            ScopeTrace scopeTrace = new ScopeTrace(dllScopeTrace.Samples, samples);
            return scopeTrace;
        }

        /// <summary>
        /// 重置数据文件。
        /// </summary>
        public void Reset()
        {
            NativeMethods.ResetDataFile(_handle);
        }


        /// <summary>
        /// 关闭数据文件。
        /// </summary>
        public void Close()
        {
            NativeMethods.CloseDataFile(_handle);
        }

        public void Dispose()
        {
            Close();
        }
    }
}