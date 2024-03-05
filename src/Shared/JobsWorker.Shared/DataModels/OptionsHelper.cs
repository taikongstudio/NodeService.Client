using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace JobsWorker.Shared.DataModels
{
    public static class OptionsHelper
    {
        public static T? ReadOptionValue<T>(this Dictionary<string, object> options, string key, Func<string, T>? func = null)
        {
            if (options.TryGetValue(key, out var value) && value is not null)
            {
                if (value is JsonElement jsonElement)
                {
                    if (jsonElement.ValueKind == JsonValueKind.String)
                    {
                        value = jsonElement.GetString();
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Number)
                    {
                        value = jsonElement.GetString();
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False)
                    {
                        value = jsonElement.GetBoolean();
                    }
                    else if (jsonElement.ValueKind == JsonValueKind.Null || jsonElement.ValueKind == JsonValueKind.Undefined)
                    {
                        value = null;
                    }
                }

                if (value is string stringValue && typeof(T).BaseType == typeof(ModelBase) && func is not null)
                {
                    return func.Invoke(stringValue);
                }
                return (T?)value;
            }
            return default(T);
        }

        public static List<T?>? ReadOptionArrayValues<T>(this Dictionary<string, object> options, string key, Func<string, T?> func)
            where T : class
        {
            if (options.TryGetValue(key, out var value) && value is not null)
            {
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                {
                    value = jsonElement.Deserialize<IEnumerable<string>>();
                }
                if (value is IEnumerable<string> stringValues)
                {
                    return stringValues.Select(func).ToList();
                }
            }
            return new List<T?>();
        }

        public static StringEntry ToStringEntry(this string? stringValue)
        {
            return new StringEntry()
            {
                Id = Guid.NewGuid().ToString(),
                Value = stringValue,
            };
        }

        public static T? FindConfig<T>(this IEnumerable<T> src, string id) where T : ModelBase
        {
            return src.FirstOrDefault(x => x.Id == id);
        }

        public static StringEntry? ToStringEntry(this ModelBase? model)
        {
            return new StringEntry()
            {
                Id = model?.Id ?? Guid.NewGuid().ToString(),
                Value = model?.Name,
            };
        }


    }
}
