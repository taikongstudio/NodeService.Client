using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessStatTool.Models
{
    public static class OptionsHelper
    {
        public static bool TryReadMySqlConfigWithIndex(this Options options, int index, out MySqlConfig mySqlConfig)
        {
            mySqlConfig = null;
            try
            {
                var mysql_host = options.args.Where(x => x.StartsWith($"mysql_host_{index}=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                var mysql_userid = options.args.Where(x => x.StartsWith($"mysql_userid_{index}=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
                var mysql_password = options.args.Where(x => x.StartsWith($"mysql_password_{index}=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries).Skip(1)).FirstOrDefault();
                var databaseString = options.args.Where(x => x.StartsWith($"mysql_database_{index}=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();

                mySqlConfig = new MySqlConfig()
                {
                    mysql_host = mysql_host,
                    mysql_userid = mysql_userid,
                    mysql_password = string.Join('=', mysql_password),
                    mysql_database = databaseString,
                };
                return mySqlConfig.mysql_host != null;
            }
            catch (Exception ex)
            {

            }
            return false;
        }


        public static MySqlConfig ReadMySqlConfig(this Options options)
        {
            var mysql_host = options.args.Where(x => x.StartsWith("mysql_host=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
            var mysql_userid = options.args.Where(x => x.StartsWith("mysql_userid=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();
            var mysql_password = options.args.Where(x => x.StartsWith("mysql_password=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries).Skip(1)).FirstOrDefault();
            var databaseString = options.args.Where(x => x.StartsWith("mysql_database=")).Select(x => x.Split("=", StringSplitOptions.RemoveEmptyEntries)[1]).FirstOrDefault();

            MySqlConfig mySqlConfig = new MySqlConfig()
            {
                mysql_host = mysql_host,
                mysql_userid = mysql_userid,
                mysql_password = string.Join('=', mysql_password),
                mysql_database = databaseString,
            };
            return mySqlConfig;
        }



    }
}
