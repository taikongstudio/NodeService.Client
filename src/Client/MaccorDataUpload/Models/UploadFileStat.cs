using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MaccorDataUpload.Models
{
    public class UploadFileStat
    {
        public UploadFileStat()
        {

        }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string file_path { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public string begin_date_time { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public string end_date_time { get; set; }

        /// <summary>
        /// 总重试次数
        /// </summary>
        public int header_data_total_retry_times { get; set; }

        /// <summary>
        /// 最大重试次数
        /// </summary>
        public int header_data_max_retry_times { get; set; }

        /// <summary>
        /// 总重试次数
        /// </summary>
        public int time_data_total_retry_times { get; set; }

        /// <summary>
        /// TimeData最大重试次数
        /// </summary>
        public int time_data_max_retry_times { get; set; }

        /// <summary>
        /// 上传TimeData数据个数
        /// </summary>
        public int time_data_uploaded_count { get; set; }

        /// <summary>
        /// 上传HeaderData数据个数
        /// </summary>
        public int header_data_uploaded_count { get; set; }

        /// <summary>
        /// 总耗时（毫秒）
        /// </summary>
        public double elapsed_milliSeconds { get; set; }

        /// <summary>
        /// TimeData总耗时（毫秒）
        /// </summary>
        public double time_data_elapsed_million_seconds { get; set; }

        /// <summary>
        /// HeaderData总耗时（毫秒）
        /// </summary>
        public double header_data_elapsed_million_seconds { get; set; }

        /// <summary>
        /// 主机名称
        /// </summary>
        public string host_name { get; set; }

        /// <summary>
        /// 是否已经发送完成。
        /// </summary>
        public bool is_completed { get; set; }

        /// <summary>
        /// 是否重新放到发送队列。
        /// </summary>
        public int repost_times { get; set; }

        public string broker_list { get; set; }

        public string hash_string { get; set; }

        public void Reset()
        {
            this.is_completed = false;
            this.begin_date_time = null;
            this.end_date_time = null;
            this.hash_string = null;
            this.header_data_elapsed_million_seconds = 0;
            this.header_data_max_retry_times = 0;
            this.header_data_total_retry_times = 0;
            this.header_data_uploaded_count = 0;
            this.time_data_elapsed_million_seconds = 0;
            this.time_data_max_retry_times = 0;
            this.time_data_total_retry_times = 0;
            this.time_data_uploaded_count = 0;
            this.elapsed_milliSeconds = 0;
        }
       
    }
}
