using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SportsData.Core.Extensions
{
    public static class EnumerableExtensions
    {
        public static async Task ForEachAsync<T>(
            this IEnumerable<T> enumerable,
            Func<T, Task> action)
        {
            foreach (T obj in enumerable)
            {
                T item = obj;
                await action(item);
                item = default(T);
            }
        }
    }
}
