using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;

using System.Collections.Concurrent;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients;

public abstract class ClientFactoryBase<TClient, TInterface>
    where TClient : class, TInterface
    where TInterface : class
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<Sport, TInterface> _clientCache = new();

    protected abstract string HttpClientName { get; }

    protected ClientFactoryBase(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger(GetType());
    }

    public TInterface Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving {ClientType} for sport: {Sport}, league: {League}, mode: {Mode}",
            typeof(TClient).Name, sport, league, mode);

        return _clientCache.GetOrAdd(mode, m =>
        {
            _logger.LogInformation("Creating new {ClientType} for mode: {Mode}", typeof(TClient).Name, m);

            var clientLogger = _loggerFactory.CreateLogger<TClient>();
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            return CreateClient(clientLogger, httpClient);
        });
    }

    protected abstract TClient CreateClient(ILogger<TClient> logger, HttpClient httpClient);
}
