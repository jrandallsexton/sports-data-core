#nullable enable
using Microsoft.Extensions.Caching.Distributed;

using System;
using System.Threading.Tasks;

namespace SportsData.Core.Extensions
{
    /// <summary>
    /// Source: IAmTimCorey @ https://www.youtube.com/watch?v=UrQWii_kfIE&t=921s
    /// </summary>
    public static class DistributedCacheExtensions
    {
        public static async Task SetRecordAsync<T>(this IDistributedCache cache,
            string recordId,
            T data,
            TimeSpan? absoluteExpireTime = null,
            TimeSpan? unusedExpireTime = null) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = absoluteExpireTime ?? TimeSpan.FromMinutes(5),
                SlidingExpiration = unusedExpireTime
            };

            var jsonData = data.ToJson();

            await cache.SetStringAsync(recordId, jsonData, options);
        }

        public static async Task<T?> GetRecordAsync<T>(this IDistributedCache cache, string recordId) where T : class
        {
            var jsonData = await cache.GetStringAsync(recordId);

            return jsonData?.FromJson<T>();
        }
    }
}
