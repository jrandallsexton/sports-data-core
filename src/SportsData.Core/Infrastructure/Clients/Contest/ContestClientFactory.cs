using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;

namespace SportsData.Core.Infrastructure.Clients.Contest;

public interface IContestClientFactory
{
    IContestClient Resolve(string sport, string league);
}

public class ContestClientFactory : IContestClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ContestClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Sport, IContestClient> _clientCache;

    public ContestClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ContestClientFactory>();
        _clientCache = new ConcurrentDictionary<Sport, IContestClient>();
    }

    public IContestClient Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving contest client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        return _clientCache.GetOrAdd(mode, m =>
        {
            var configKey = CommonConfigKeys.GetContestProviderUri(m);
            var apiUrl = _configuration[configKey];

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("Missing Contest API URL for mode {Mode}. Config key: {ConfigKey}", m, configKey);
                throw new InvalidOperationException($"Contest API URL not configured for mode {m}. Config key: {configKey}");
            }

            _logger.LogInformation("Creating contest client for mode {Mode} with base URL: {ApiUrl}", m, apiUrl);

            var httpClient = _httpClientFactory.CreateClient($"ContestClient_{m}");
            httpClient.BaseAddress = new Uri(apiUrl);

            var contestLogger = _loggerFactory.CreateLogger<ContestClient>();
            return new ContestClient(contestLogger, httpClient);
        });
    }
}
