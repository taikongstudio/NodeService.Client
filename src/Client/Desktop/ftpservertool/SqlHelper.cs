using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace ftpservertool
{
    internal class SqlHelper
    {
        public static async Task<bool> InsertOrUpdate(MySqlConfig mySqlConfig, IEnumerable<MachineInfo> machineInfoList, Action<string> logger)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(mySqlConfig, nameof(mySqlConfig));
                ArgumentNullException.ThrowIfNull(machineInfoList, nameof(machineInfoList));
                string cs = $"server={mySqlConfig.mysql_host};" +
                    $"userid={mySqlConfig.mysql_userid};" +
                    $"password={mySqlConfig.mysql_password};" +
                    $"database={mySqlConfig.mysql_database};" +
                    $"charset=utf8;";

                using var connection = new MySqlConnection(cs);
                connection.Open();
                foreach (var machineInfo in machineInfoList)
                {
                    if (await exists(connection, machineInfo))
                    {
                        await update(connection, machineInfo);
                        Console.WriteLine($"updated {machineInfo.computer_name}");
                    }
                    else
                    {
                        await Insert(connection, machineInfo);
                        Console.WriteLine($"inserted {machineInfo.computer_name}");
                    }
                }

            }
            catch (Exception ex)
            {
                logger?.Invoke(ex.ToString());
            }
            return false;

        }

        private static async Task<bool> setnames(MySqlConnection mySqlConnection)
        {
            var stm = "set names gb2312";
            using var cmd = new MySqlCommand(stm, mySqlConnection);
            var count = await cmd.ExecuteNonQueryAsync();
            return count > 0;
        }

        private static async Task<bool> Insert(MySqlConnection mySqlConnection, MachineInfo machineinfo)
        {
            var stm = "insert into machine_info (factory_name,host_name, test_info, lab_area, lab_name, computer_name,login_name,install_status,usages,remarks)" +
    "values (@factory_name,@host_name,@test_info,@lab_area,@lab_name,@computer_name,@login_name,@install_status,@usages,@remarks);";
            using var cmd = new MySqlCommand(stm, mySqlConnection);
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.factory_name), machineinfo.factory_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.host_name), machineinfo.host_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.test_info), machineinfo.test_info));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.lab_area), machineinfo.lab_area));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.lab_name), machineinfo.lab_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.computer_name), machineinfo.computer_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.login_name), machineinfo.login_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.install_status), machineinfo.install_status));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.usages), machineinfo.usages));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.remarks), machineinfo.remarks));
            var count = await cmd.ExecuteNonQueryAsync();
            return count > 0;
        }

        private static async Task<bool> exists(MySqlConnection mySqlConnection, MachineInfo machineinfo)
        {
            var stm = "select count(*) from machine_info where computer_name=@computer_name;";
            using var cmd = new MySqlCommand(stm, mySqlConnection);
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.computer_name), machineinfo.computer_name));
            var count = await cmd.ExecuteScalarAsync();
            return ((long)count) > 0;
        }

        private static async Task<bool> update(MySqlConnection mySqlConnection, MachineInfo machineinfo)
        {
            var stm = "update machine_info set factory_name=@factory_name, test_info=@test_info, lab_area=@lab_area, " +
                "lab_name=@lab_name, computer_name=@computer_name, login_name=@login_name, install_status=@install_status, usages=@usages,remarks=@remarks " +
                "where computer_name =@computer_name;";
            using var cmd = new MySqlCommand(stm, mySqlConnection);
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.factory_name), machineinfo.factory_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.host_name), machineinfo.host_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.test_info), machineinfo.test_info));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.lab_area), machineinfo.lab_area));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.lab_name), machineinfo.lab_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.computer_name), machineinfo.computer_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.login_name), machineinfo.login_name));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.install_status), machineinfo.install_status));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.usages), machineinfo.usages));
            cmd.Parameters.Add(new MySqlParameter(nameof(machineinfo.remarks), machineinfo.remarks));
            var count = await cmd.ExecuteNonQueryAsync();
            return count > 0;
        }

        public static List<MachineInfo> GetMachineInfoList(MySqlConfig mySqlConfig)
        {
            List<MachineInfo> machineinfos = new List<MachineInfo>();
            try
            {
                var stm = "select * from machine_info";
                string cs = $"server={mySqlConfig.mysql_host};userid={mySqlConfig.mysql_userid};password={mySqlConfig.mysql_password};database={mySqlConfig.mysql_database}";

                using var connection = new MySqlConnection(cs);
                connection.Open();
                using var cmd = new MySqlCommand(stm, connection);

                using (var reader = cmd.ExecuteReader())
                {
                    int factory_name_index = reader.GetOrdinal(nameof(MachineInfo.factory_name));
                    int host_name_index = reader.GetOrdinal(nameof(MachineInfo.host_name));
                    int test_info_index = reader.GetOrdinal(nameof(MachineInfo.test_info));
                    int lab_area_index = reader.GetOrdinal(nameof(MachineInfo.lab_area));
                    int lab_name_index = reader.GetOrdinal(nameof(MachineInfo.lab_name));
                    int computer_name_index = reader.GetOrdinal(nameof(MachineInfo.computer_name));
                    int login_name_index = reader.GetOrdinal(nameof(MachineInfo.login_name));
                    int install_status_index = reader.GetOrdinal(nameof(MachineInfo.install_status));
                    while (reader.Read())
                    {
                        MachineInfo machineinfo = new MachineInfo();
                        machineinfo.factory_name = reader.GetString(factory_name_index);
                        machineinfo.computer_name = reader.GetString(computer_name_index);
                        if (!reader.IsDBNull(host_name_index))
                        {
                            machineinfo.host_name = reader.GetString(host_name_index);
                        }
                        if (!reader.IsDBNull(test_info_index))
                        {
                            machineinfo.test_info = reader.GetString(test_info_index);
                        }
                        if (!reader.IsDBNull(lab_area_index))
                        {
                            machineinfo.lab_area = reader.GetString(lab_area_index);
                        }
                        if (!reader.IsDBNull(lab_name_index))
                        {
                            machineinfo.lab_name = reader.GetString(lab_name_index);
                        }
                        if (!reader.IsDBNull(login_name_index))
                        {
                            machineinfo.login_name = reader.GetString(login_name_index);
                        }

                        machineinfo.install_status = reader.GetBoolean(install_status_index);
                        machineinfos.Add(machineinfo);

                    }
                } 



            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return machineinfos;
        }

        public static List<MachineInfo> GetMachineInfoExt1List(MySqlConfig mySqlConfig)
        {
            List<MachineInfo> machineinfos = new List<MachineInfo>();
            try
            {
                var stm = "select * from machine_info_ext1";
                string cs = $"server={mySqlConfig.mysql_host};userid={mySqlConfig.mysql_userid};password={mySqlConfig.mysql_password};database={mySqlConfig.mysql_database}";

                using var connection = new MySqlConnection(cs);
                connection.Open();
                using var cmd = new MySqlCommand(stm, connection);

                using (var reader = cmd.ExecuteReader())
                {
                    int computer_name_index = reader.GetOrdinal(nameof(MachineInfo.computer_name));
                    int usage_index = reader.GetOrdinal(nameof(MachineInfo.usages));
                    int remarks_index = reader.GetOrdinal(nameof(MachineInfo.remarks));
                    int factory_name_index = reader.GetOrdinal(nameof(MachineInfo.factory_name));
                    while (reader.Read())
                    {
                        MachineInfo machineinfo = new MachineInfo();
                        machineinfo.computer_name = reader.GetString(computer_name_index);
                        if (!reader.IsDBNull(usage_index))
                        {
                            machineinfo.usages = reader.GetString(usage_index);
                        }

                        if (!reader.IsDBNull(remarks_index))
                        {
                            machineinfo.remarks = reader.GetString(remarks_index);
                        }

                        if (!reader.IsDBNull(factory_name_index))
                        {
                            machineinfo.factory_name = reader.GetString(factory_name_index);
                        }

                        machineinfos.Add(machineinfo);

                    }
                }



            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return machineinfos;
        }

        public static async Task<List<MachineInfo>> GetMarccorStatusAsync(MySqlConfig mySqlConfig)
        {
            List<MachineInfo> machineinfos = new List<MachineInfo>();
            try
            {
                var stm = "select host_name as 主机名称 ,(select factory_name from machine_info where lower(computer_name)=lower(maccor_data.host_name)) as 区域," +
                    "date(begin_date_time) as 上传日期," +
                    "sum(header_data_uploaded_count) as 头部数据上传总个数," +
                    "sum(time_data_uploaded_count) as 时间数据上传总个数," +
                    "sum(header_data_total_retry_times) as 头部数据上传重试总次数,\r\n       sum(time_data_total_retry_times) as 时间数据上传重试总次数,\r\n       sum(header_data_elapsed_million_seconds) as 时间数据上传总时间ms,\r\n       sum(time_data_elapsed_million_seconds) as 时间数据上传总时间ms\r\nfrom maccor_data where date(begin_date_time)='2024-01-03'\r\ngroup by host_name,date(begin_date_time);";
                string cs = $"server={mySqlConfig.mysql_host};userid={mySqlConfig.mysql_userid};password={mySqlConfig.mysql_password};database={mySqlConfig.mysql_database}";

                using var connection = new MySqlConnection(cs);
                connection.Open();
                using var cmd = new MySqlCommand(stm, connection);

                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int factory_name_index = reader.GetOrdinal(nameof(MachineInfo.factory_name));
                        int host_name_index = reader.GetOrdinal(nameof(MachineInfo.host_name));
                        int test_info_index = reader.GetOrdinal(nameof(MachineInfo.test_info));
                        int lab_area_index = reader.GetOrdinal(nameof(MachineInfo.lab_area));
                        int lab_name_index = reader.GetOrdinal(nameof(MachineInfo.lab_name));
                        int computer_name_index = reader.GetOrdinal(nameof(MachineInfo.computer_name));
                        int login_name_index = reader.GetOrdinal(nameof(MachineInfo.login_name));
                        int install_status_index = reader.GetOrdinal(nameof(MachineInfo.install_status));
                        MachineInfo machineinfo = new MachineInfo();
                        machineinfo.factory_name = reader.GetString(factory_name_index);
                        machineinfo.host_name = reader.GetString(host_name_index);
                        machineinfo.test_info = reader.GetString(test_info_index);
                        machineinfo.lab_area = reader.GetString(lab_area_index);
                        machineinfo.lab_name = reader.GetString(lab_name_index);
                        machineinfo.login_name = reader.GetString(login_name_index);
                        machineinfo.install_status = reader.GetBoolean(install_status_index);
                        machineinfos.Add(machineinfo);

                    }
                }



            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
            return machineinfos;
        }

    }
}
