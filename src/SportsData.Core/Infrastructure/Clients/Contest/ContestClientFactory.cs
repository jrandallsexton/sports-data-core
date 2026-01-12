using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;

using System;
using System.Collections.Concurrent;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Contest;

public interface IContestClientFactory
{
    IProvideContests Resolve(string sport, string league);
}

public class ContestClientFactory : IContestClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ContestClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Sport, IProvideContests> _clientCache;

    public ContestClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ContestClientFactory>();
        _clientCache = new ConcurrentDictionary<Sport, IProvideContests>();
    }

    public IProvideContests Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving contest client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        return _clientCache.GetOrAdd(mode, m =>
        {
            _logger.LogInformation("Creating contest client for mode {Mode}", m);

            var httpClient = _httpClientFactory.CreateClient($"{HttpClients.ContestClient}");
            var contestLogger = _loggerFactory.CreateLogger<ContestClient>();
            return new ContestClient(contestLogger, httpClient);
        });
    }
}
