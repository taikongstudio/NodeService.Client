using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NodeService.ServiceHost.Helpers
{
    internal class CryptographyHelper
    {
        public static string CalculateFileMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static string CalculateStreamMD5(Stream stream)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static async Task<string?> SHA256_HashStringAsync(string value)
        {
            using var stream = new MemoryStream();
            using var streamWriter = new StreamWriter(stream);
            streamWriter.Write(value);
            streamWriter.Flush();
            stream.Position = 0;
            string hash = await SHA256_HashStreamAsync(stream);
            return hash;
        }

        public static async Task<string> SHA256_HashStreamAsync(Stream stream)
        {
            var bytes = await SHA256.HashDataAsync(stream);
            var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            return hash;
        }
    }
}
