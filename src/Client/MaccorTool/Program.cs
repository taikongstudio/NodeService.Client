using DataFileCollector.Common;
using MySql.Data.MySqlClient;

namespace MaccorTool
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
            DataFileReader.TryLoad(@"D:\Maccor\00PCBGPN01C02DD8V0000283-AAA.003",null,out Exception ex, out var reader);
            foreach (var item in reader.EnumDataFileHeaders())
            {
                var str = item.AsJsonString();
                Console.WriteLine(str);
            }

            foreach (var item in reader.EnumTimeDatas())
            {
                var str = item.AsJsonString();
                Console.WriteLine(str);
            }
            
        }
    }
}
