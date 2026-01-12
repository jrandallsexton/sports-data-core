using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;

using System.Collections.Concurrent;
using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Venue;

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

        // TODO: Revisit when launch multi-sport
        return _clientCache.GetOrAdd(mode, m =>
        {
            _logger.LogInformation("Creating new VenueClient for mode: {Mode}", m);

            var logger = _loggerFactory.CreateLogger<VenueClient>();
            var clientName = $"{HttpClients.VenueClient}";
            var httpClient = _httpClientFactory.CreateClient(clientName);

            return new VenueClient(logger, httpClient);
        });
    }
}
