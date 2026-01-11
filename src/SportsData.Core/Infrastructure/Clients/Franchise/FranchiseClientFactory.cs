using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Franchise;

using System;
using System.Collections.Generic;
using System.Net.Http;

public interface IFranchiseClientFactory
{
    IProvideFranchises Resolve(string sport, string league);
}

public class FranchiseClientFactory : IFranchiseClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FranchiseClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<Sport, IProvideFranchises> _clientCache;

    public FranchiseClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<FranchiseClientFactory>();
        _clientCache = new Dictionary<Sport, IProvideFranchises>();
    }

    public IProvideFranchises Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving franchise client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        if (!_clientCache.TryGetValue(mode, out var client))
        {
            var configKey = CommonConfigKeys.GetFranchiseProviderUri();
            var apiUrl = _configuration[configKey];

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("Missing API URL for mode {Mode}. Config key: {ConfigKey}", mode, configKey);
                throw new InvalidOperationException($"Missing configuration for franchise client: {mode}");
            }

            _logger.LogInformation("Creating new FranchiseClient for mode: {Mode}", mode);

            var logger = _loggerFactory.CreateLogger<FranchiseClient>();
            var clientName = $"{HttpClients.FranchiseClient}";
            var httpClient = _httpClientFactory.CreateClient(clientName);

            client = new FranchiseClient(logger, httpClient);
            _clientCache[mode] = client;
        }

        return client;
    }
}
