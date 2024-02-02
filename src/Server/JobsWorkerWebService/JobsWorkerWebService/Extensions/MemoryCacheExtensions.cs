using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace JobsWorkerWebService.Extensions
{
    public static class MemoryCacheExtensions
    {

        public static async Task<ConcurrentDictionary<string, string>?> GetOrCreateNodePropsAsync(this IMemoryCache memoryCache, string nodeName)
        {
            string key = $"NodeProperties:{nodeName}";
            var propDict = await memoryCache.GetOrCreateAsync<ConcurrentDictionary<string, string>>(key, (cacheEntry) =>
            {
                var dict = new ConcurrentDictionary<string, string>();
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return Task.FromResult(dict);
            });
            return propDict;
        }


    }
}
