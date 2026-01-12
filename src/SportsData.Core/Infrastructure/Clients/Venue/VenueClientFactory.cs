using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Config;
using SportsData.Core.Infrastructure.Clients;
using SportsData.Core.Infrastructure.Clients.Venue;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;

public interface IVenueClientFactory
{
    IProvideVenues Resolve(string sport, string league);
}

public class VenueClientFactory : IVenueClientFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VenueClientFactory> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<Sport, IProvideVenues> _clientCache;

    public VenueClientFactory(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<VenueClientFactory>();
        _clientCache = new ConcurrentDictionary<Sport, IProvideVenues>();
    }

    public IProvideVenues Resolve(string sport, string league)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        _logger.LogDebug("Resolving venue client for sport: {Sport}, league: {League}, mode: {Mode}",
            sport, league, mode);

        return _clientCache.GetOrAdd(mode, m =>
        {
            var configKey = CommonConfigKeys.GetVenueProviderUri();
            var apiUrl = _configuration[configKey];

            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                _logger.LogError("Missing API URL for mode {Mode}. Config key: {ConfigKey}", m, configKey);
                throw new InvalidOperationException($"Missing configuration for venue client: {m}");
            }

            _logger.LogInformation("Creating new VenueClient for mode: {Mode}", m);

            var logger = _loggerFactory.CreateLogger<VenueClient>();
            var clientName = $"{HttpClients.VenueClient}";
            var httpClient = _httpClientFactory.CreateClient(clientName);

            return new VenueClient(logger, httpClient);
        });
    }
}
