using System;
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
    private readonly Dictionary<Sport, IContestClient> _clientCache;

    public ContestClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ContestClientFactory>();
        _clientCache = new Dictionary<Sport, IContestClient>();
    }

    public IContestClient Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving contest client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        if (!_clientCache.TryGetValue(mode, out var client))
        {
            var configKey = CommonConfigKeys.GetContestProviderUri(mode);
            var apiUrl = _configuration[configKey];

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("Missing Contest API URL for mode {Mode}. Config key: {ConfigKey}", mode, configKey);
                throw new InvalidOperationException($"Contest API URL not configured for mode {mode}. Config key: {configKey}");
            }

            _logger.LogInformation("Creating contest client for mode {Mode} with base URL: {ApiUrl}", mode, apiUrl);

            var httpClient = _httpClientFactory.CreateClient($"ContestClient_{mode}");
            httpClient.BaseAddress = new Uri(apiUrl);

            client = new ContestClient(httpClient);
            _clientCache[mode] = client;
        }

        return client;
    }
}
