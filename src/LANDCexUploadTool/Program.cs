using LANDCexSharp;
using System.Text.Json;

namespace LANDCexTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string path = "D:\\xwd\\蓝电数据接口文件\\landan\\M20220409026-1\\_029_5.cex";
            if (!LANDCexDataReader.CreateReader(path, USCH.BaseOn_mA, out var dataReader))
            {
                return;
            }
            var briefInfoReader = new BriefInfoReader(path);
            var brief = briefInfoReader.Read();

            foreach (var cycle in dataReader.EnumCycles())
            {
                Console.WriteLine(JsonSerializer.Serialize(cycle, new JsonSerializerOptions()
                {
                    IncludeFields = true,
                }));
                foreach (var rec in dataReader.EnumRecs(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(rec, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }
                foreach (var step in dataReader.EnumSteps(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(step, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }
                foreach (var step in dataReader.EnumDischSteps(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(step, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }
                foreach (var step in dataReader.EnumChargeSteps(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(step, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }

                foreach (var step in dataReader.EnumDischRecs(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(step, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }

                foreach (var step in dataReader.EnumChargeRecs(cycle.Index))
                {
                    Console.WriteLine(JsonSerializer.Serialize(step, new JsonSerializerOptions()
                    {
                        IncludeFields = true,
                    }));
                }

            }


            Console.WriteLine("Hello, World!");
        }
    }
}
