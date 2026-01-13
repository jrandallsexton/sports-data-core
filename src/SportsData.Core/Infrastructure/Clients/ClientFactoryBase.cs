using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

using System;
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

    protected readonly IConfiguration? Configuration;
    protected abstract string HttpClientName { get; }

    protected ClientFactoryBase(
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration? configuration = null)
    {
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger(GetType());
        Configuration = configuration;
    }

    public TInterface Resolve(Sport mode)
    {
        return _clientCache.GetOrAdd(mode, m =>
        {
            _logger.LogInformation("Creating new {ClientType} for mode: {Mode}", typeof(TClient).Name, m);

            var clientLogger = _loggerFactory.CreateLogger<TClient>();
            var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            // Allow derived classes to provide mode-specific base URL
            var baseUrl = GetBaseAddressForMode(m);
            if (baseUrl != null)
            {
                httpClient.BaseAddress = baseUrl;
            }

            return CreateClient(clientLogger, httpClient);
        });
    }

    /// <summary>
    /// Override in derived classes to provide mode-specific base URLs.
    /// Return null to use the pre-configured HttpClient base address.
    /// </summary>
    protected virtual Uri? GetBaseAddressForMode(Sport mode) => null;

    protected abstract TClient CreateClient(ILogger<TClient> logger, HttpClient httpClient);
}
