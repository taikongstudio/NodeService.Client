using MaccorUploadTool.Models;
using MySql.Data.MySqlClient;

namespace MaccorUploadTool.Helper
{
    internal class SqlHelper
    {
        public static async Task<bool> InsertStat(MySqlConfig mySqlConfig, UploadFileStat uploadFileStat, Action<string> logger)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mySqlConfig, nameof(mySqlConfig));
                ArgumentNullException.ThrowIfNull(uploadFileStat, nameof(uploadFileStat));
                string cs = $"server={mySqlConfig.mysql_host};userid={mySqlConfig.mysql_userid};password={mySqlConfig.mysql_password};database={mySqlConfig.mysql_database}";

                using var con = new MySqlConnection(cs);
                con.Open();

                var stm = "insert into maccor_data (host_name, file_path, begin_date_time, end_date_time, header_data_total_retry_times," +
                    "header_data_max_retry_times, time_data_total_retry_times, time_data_max_retry_times," +
                    "time_data_uploaded_count, header_data_uploaded_count, elapsed_milliSeconds," +
                    "time_data_elapsed_million_seconds, header_data_elapsed_million_seconds, repost_times, is_completed, broker_list)" +
                    "values (@host_name,@file_path,@begin_date_time,@end_date_time,@header_data_total_retry_times,@header_data_max_retry_times," +
                    "@time_data_total_retry_times,@time_data_max_retry_times,@time_data_uploaded_count,@header_data_uploaded_count," +
                    "@elapsed_milliSeconds,@time_data_elapsed_million_seconds,@header_data_elapsed_million_seconds,@repost_times,@is_completed,@broker_list);";
                using var cmd = new MySqlCommand(stm, con);
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.host_name), uploadFileStat.host_name));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.file_path), uploadFileStat.file_path));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.begin_date_time), uploadFileStat.begin_date_time));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.end_date_time), uploadFileStat.end_date_time));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.header_data_total_retry_times), uploadFileStat.header_data_total_retry_times));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.header_data_max_retry_times), uploadFileStat.header_data_max_retry_times));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.time_data_total_retry_times), uploadFileStat.time_data_total_retry_times));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.time_data_max_retry_times), uploadFileStat.time_data_max_retry_times));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.time_data_uploaded_count), uploadFileStat.time_data_uploaded_count));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.header_data_uploaded_count), uploadFileStat.header_data_uploaded_count));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.elapsed_milliSeconds), uploadFileStat.elapsed_milliSeconds));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.time_data_elapsed_million_seconds), uploadFileStat.time_data_elapsed_million_seconds));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.header_data_elapsed_million_seconds), uploadFileStat.header_data_elapsed_million_seconds));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.repost_times), uploadFileStat.repost_times));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.is_completed), uploadFileStat.is_completed));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.broker_list), uploadFileStat.broker_list));
                var count = await cmd.ExecuteNonQueryAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                logger?.Invoke(ex.ToString());
            }
            return false;

        }

    }
}
