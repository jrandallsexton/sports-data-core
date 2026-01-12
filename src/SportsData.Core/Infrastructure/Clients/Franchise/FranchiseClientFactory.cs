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

namespace SportsData.Core.Infrastructure.Clients.Franchise;

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
    private readonly ConcurrentDictionary<Sport, IProvideFranchises> _clientCache;

    public FranchiseClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<FranchiseClientFactory>();
        _clientCache = new ConcurrentDictionary<Sport, IProvideFranchises>();
    }

    public IProvideFranchises Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving franchise client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        return _clientCache.GetOrAdd(mode, m =>
        {
            _logger.LogInformation("Creating new FranchiseClient for mode: {Mode}", m);

            var logger = _loggerFactory.CreateLogger<FranchiseClient>();
            var clientName = $"{HttpClients.FranchiseClient}";
            var httpClient = _httpClientFactory.CreateClient(clientName);

            return new FranchiseClient(logger, httpClient);
        });
    }
}
