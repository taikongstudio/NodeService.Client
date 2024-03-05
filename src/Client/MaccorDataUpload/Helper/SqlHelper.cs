using JobsWorker.Shared.DataModels;
using MaccorDataUpload.Models;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace MaccorDataUpload.Helper
{
    internal class SqlHelper
    {
        public static async Task<int> UpdateHashString(
            MysqlConfigModel mysqlConfig,
            string host_name,
            string file_path,
            string hash_string,
            ILogger logger)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mysqlConfig, nameof(mysqlConfig));
                ArgumentNullException.ThrowIfNull(host_name, nameof(host_name));
                ArgumentNullException.ThrowIfNull(file_path, nameof(file_path));
                ArgumentNullException.ThrowIfNull(hash_string, nameof(hash_string));
                string cs = $"server={mysqlConfig.Host};port={mysqlConfig.Port};userid={mysqlConfig.UserId};password={mysqlConfig.Password};database={mysqlConfig.Database}";

                using var con = new MySqlConnection(cs);
                con.Open();

                var stm = "update maccor_data set hash_string = @hash_string where host_name = @host_name and file_path = @file_path;";
                using var cmd = new MySqlCommand(stm, con);
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.host_name), host_name));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.file_path), file_path));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.hash_string), hash_string));
                var count = await cmd.ExecuteNonQueryAsync();
                return count;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
            return -1;
        }

        public static async Task<bool> CheckHashString(MysqlConfigModel mysqlConfig, UploadFileStat uploadFileStat, ILogger logger)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mysqlConfig, nameof(mysqlConfig));
                ArgumentNullException.ThrowIfNull(uploadFileStat, nameof(uploadFileStat));
                string cs = $"server={mysqlConfig.Host};userid={mysqlConfig.UserId};password={mysqlConfig.Password};database={mysqlConfig.Database}";

                using var con = new MySqlConnection(cs);
                con.Open();

                var stm = "select count(*) from maccor_data where hash_string is not null and hash_string = @hash_string and host_name = @host_name and file_path = @file_path;";
                using var cmd = new MySqlCommand(stm, con);
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.host_name), uploadFileStat.host_name));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.file_path), uploadFileStat.file_path));
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.hash_string), uploadFileStat.hash_string));
                var count = await cmd.ExecuteNonQueryAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
            return false;
        }

        public static async Task<bool> InsertStat(MysqlConfigModel mysqlConfig, UploadFileStat uploadFileStat, ILogger logger)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mysqlConfig, nameof(mysqlConfig));
                ArgumentNullException.ThrowIfNull(uploadFileStat, nameof(uploadFileStat));
                string cs = $"server={mysqlConfig.Host};userid={mysqlConfig.UserId};password={mysqlConfig.Password};database={mysqlConfig.Database}";

                using var con = new MySqlConnection(cs);
                con.Open();

                var stm = "insert into maccor_data (host_name, file_path, begin_date_time, end_date_time, header_data_total_retry_times," +
                    "header_data_max_retry_times, time_data_total_retry_times, time_data_max_retry_times," +
                    "time_data_uploaded_count, header_data_uploaded_count, elapsed_milliSeconds," +
                    "time_data_elapsed_million_seconds, header_data_elapsed_million_seconds, repost_times, is_completed, broker_list,hash_string)" +
                    "values (@host_name,@file_path,@begin_date_time,@end_date_time,@header_data_total_retry_times,@header_data_max_retry_times," +
                    "@time_data_total_retry_times,@time_data_max_retry_times,@time_data_uploaded_count,@header_data_uploaded_count," +
                    "@elapsed_milliSeconds,@time_data_elapsed_million_seconds,@header_data_elapsed_million_seconds,@repost_times,@is_completed,@broker_list,@hash_string);";
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
                cmd.Parameters.Add(new MySqlParameter(nameof(UploadFileStat.hash_string), uploadFileStat.hash_string));
                var count = await cmd.ExecuteNonQueryAsync();
                return count > 0;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
            return false;

        }

    }
}
