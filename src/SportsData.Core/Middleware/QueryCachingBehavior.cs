using MediatR;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common.Queries;
using SportsData.Core.Extensions;

using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware
{
    public sealed class QueryCachingBehavior<TRequest, TResponse> :
        IPipelineBehavior<TRequest, TResponse> where TRequest : CacheableQuery<TResponse> where TResponse : class
    {
        private readonly ILogger<QueryCachingBehavior<TRequest, TResponse>> _logger;
        private readonly IDistributedCache _cache;

        public QueryCachingBehavior(
            ILogger<QueryCachingBehavior<TRequest, TResponse>> logger,
            IDistributedCache cache)
        {
            _logger = logger;
            _cache = cache;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            TResponse response;
            if (request.BypassCache) return await next();

            var type = request.GetType();
            var cacheKey = type.FullName ?? type.Name;

            if (type.FullName is null)
                _logger.LogWarning("Type.FullName was null for request of type {Type}", type);

            var cachedResponse = await _cache.GetRecordAsync<TResponse>(cacheKey);

            if (cachedResponse != null)
            {
                _logger.LogInformation("Fetched from Cache: {cacheKey}", cacheKey);
            }
            else
            {
                cachedResponse = await GetResponseAndAddToCache();
                _logger.LogInformation("Added to Cache: {cacheKey}", cacheKey);
            }
            return cachedResponse;

            async Task<TResponse> GetResponseAndAddToCache()
            {
                response = await next();
                await _cache.SetRecordAsync(cacheKey, response);
                return response;
            }
        }
    }
}
