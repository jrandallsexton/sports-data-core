using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Clients;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.FranchiseSeason;

public interface IFranchiseSeasonClientFactory
{
    IProvideFranchiseSeasons Resolve(string sport, string league);
}

public class FranchiseSeasonClientFactory : IFranchiseSeasonClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FranchiseSeasonClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Sport, IProvideFranchiseSeasons> _clientCache;

    public FranchiseSeasonClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<FranchiseSeasonClientFactory>();
        _clientCache = new ConcurrentDictionary<Sport, IProvideFranchiseSeasons>();
    }

    public IProvideFranchiseSeasons Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving franchise season client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        // TODO: Revisit when launch multi-sport
        return _clientCache.GetOrAdd(mode, m =>
        {
            var configKey = CommonConfigKeys.GetFranchiseProviderUri();
            var apiUrl = _configuration[configKey];

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("Missing API URL for mode {Mode}. Config key: {ConfigKey}", m, configKey);
                throw new InvalidOperationException($"Missing configuration for franchise season client: {m}");
            }

            _logger.LogInformation("Creating new FranchiseSeasonClient for mode: {Mode}", m);

            var logger = _loggerFactory.CreateLogger<FranchiseSeasonClient>();
            var clientName = $"{HttpClients.FranchiseClient}"; // Same base URL as FranchiseClient
            var httpClient = _httpClientFactory.CreateClient(clientName);

            return new FranchiseSeasonClient(logger, httpClient);
        });
    }
}
