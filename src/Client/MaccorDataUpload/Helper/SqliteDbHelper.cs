using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Xml.Linq;
using MySql.Data.MySqlClient;

namespace MaccorDataUpload.Helper
{
    internal static class SqliteDbHelper
    {
        public static void Initialize(string dbName, ILogger logger)
        {
            try
            {
                using (var sqliteConnection = CreateConnection(dbName, logger))
                {
                    sqliteConnection.Open();
                    using (var cmd = sqliteConnection.CreateCommand())
                    {
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS file_records(FilePath TEXT NOT NULL,HASH TEXT NOT NULL);";
                        if (cmd.ExecuteNonQuery() == 0)
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());

            }
        }

        private static SqliteConnection CreateConnection(string dbName, ILogger logger)
        {
            string configDir = GetConfigDirectory(logger);
            var db_path = Path.Combine(configDir, dbName);
            logger.LogInformation($"ConfigDir:{configDir},db path:{db_path}");
            SqliteConnection sqliteConnection = new SqliteConnection($"Data Source={db_path};");
            return sqliteConnection;
        }

        private static string GetConfigDirectory(ILogger logger)
        {
            string configDir = null;
            try
            {
                var docPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments, Environment.SpecialFolderOption.Create);
                configDir = Path.Combine(docPath, "MaccorUploadConfig");
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
            if (configDir == null)
            {
                try
                {
                    foreach (var driverInfo in DriveInfo.GetDrives())
                    {
                        if (driverInfo.Name.StartsWith("C:\\"))
                        {
                            logger.LogInformation($"Skip driver {driverInfo.Name}");
                            continue;
                        }
                        configDir = Path.Combine(driverInfo.Name, "MaccorUploadConfig", Dns.GetHostName());
                        try
                        {
                            if (!Directory.Exists(configDir))
                            {
                                Directory.CreateDirectory(configDir);
                            }
                            break;
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.ToString());
                }


            }

            return configDir;
        }

        public static bool ExistsFileRecord(string filePath, string dbName, ILogger logger, out string hash)
        {
            hash = null;
            try
            {
                string md5 = MD5Helper.CalculateFileMD5(filePath);
                hash = md5.ToString();
                using (var sqliteConnection = CreateConnection(dbName, logger))
                {
                    sqliteConnection.Open();
                    using (var cmd = sqliteConnection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT COUNT(*) FROM file_records WHERE file_records.FilePath =@FilePath and file_records.Hash=@Hash";
                        cmd.Parameters.AddWithValue("@FilePath", md5);
                        cmd.Parameters.AddWithValue("@Hash", md5);
                        var result = (long)cmd.ExecuteScalar();
                        if (result >= 1)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (SqliteException ex)
            {
                logger.LogError(ex.ToString());
                try
                {
                    string configDir = GetConfigDirectory(logger);

                    var db_path = Path.Combine(configDir, dbName);
                    if (File.Exists(db_path))
                    {
                        File.Delete(db_path);
                        logger.LogInformation($"Delete database:{db_path}");
                        return false;
                    }
                }
                catch (Exception ex1)
                {
                    logger.LogError(ex.ToString());
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());

            }
            return false;
        }

        public static bool InsertFileRecord(string filePath, string dbName, ILogger logger)
        {
            try
            {
                string md5 = MD5Helper.CalculateFileMD5(filePath);
                using (var sqliteConnection = CreateConnection(dbName, logger))
                {
                    sqliteConnection.Open();
                    using (var cmd = sqliteConnection.CreateCommand())
                    {
                        cmd.CommandText = $"INSERT INTO file_records(FilePath,Hash) VALUES(@FilePath,@HASH)";
                        cmd.Parameters.AddWithValue("@FilePath", filePath);
                        cmd.Parameters.AddWithValue("@HASH", md5);
                        if (cmd.ExecuteNonQuery() >= 1)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
            }
            return false;
        }



    }
}
