﻿using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Notification
{
    public interface IProvideNotifications : IProvideHealthChecks
    {

    }

    public class NotificationProvider : IProvideNotifications
    {
        private readonly ILogger<NotificationProvider> _logger;
        private readonly HttpClient _httpClient;

        public NotificationProvider(
            ILogger<NotificationProvider> logger,
            IHttpClientFactory clientFactory)
        {
            _logger = logger;
            _httpClient = clientFactory.CreateClient(HttpClients.NotificationClient);
        }
        public string GetProviderName()
        {
            return HttpClients.NotificationClient;
        }

        public async Task<Dictionary<string, object>> GetHealthStatus()
        {
            try
            {
                var response = await _httpClient.GetAsync("/health");
                var tmp = response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return new Dictionary<string, object>()
                {
                    { "status", response.StatusCode }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>()
                {
                    { "status", HttpStatusCode.ServiceUnavailable }
                };
            }
        }
    }
}
