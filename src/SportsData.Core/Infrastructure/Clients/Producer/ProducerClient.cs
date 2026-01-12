using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {
        Task<VenueDto?> GetVenue(string id);
        Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);
        Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear);
        Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId);
        Task RefreshContestByContestId(Guid contestId);
        Task RefreshContestMediaByContestId(Guid contestId);
        Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear);
    }

    public class ProducerClient : ClientBase, IProvideProducers
    {
        private readonly ILogger<ProducerClient> _logger;

        public ProducerClient(
            ILogger<ProducerClient> logger,
            HttpClient httpClient) :
            base(httpClient)
        {
            _logger = logger;
        }

        public async Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear)
        {
            var response = await HttpClient.GetAsync($"franchise-seasons/seasonYear/{seasonYear}/metrics");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var metrics = tmp.FromJson<List<FranchiseSeasonMetricsDto>>();
            return metrics ?? [];
        }

        public async Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId)
        {
            var response = await HttpClient.GetAsync($"franchise-seasons/id/{franchiseSeasonId}/metrics");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var metrics = tmp.FromJson<FranchiseSeasonMetricsDto>();
            return metrics ?? new FranchiseSeasonMetricsDto();
        }

        public async Task<VenueDto?> GetVenue(string id)
        {
            var response = await HttpClient.GetAsync($"venues/{id}");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var venue = tmp.FromJson<Success<VenueDto>>();

            return venue?.Value;
        }

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId)
        {
            var response = await HttpClient.GetAsync($"contests/{contestId}/overview");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var overview = tmp.FromJson<ContestOverviewDto>();

            return overview ?? new ContestOverviewDto();
        }

        public async Task RefreshContestByContestId(Guid contestId)
        {
            var content = new StringContent(contestId.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"contests/{contestId}/update", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task RefreshContestMediaByContestId(Guid contestId)
        {
            var content = new StringContent(contestId.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"contests/{contestId}/media/refresh", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear)
        {
            _logger.LogInformation(
                "ProducerClient.GetFranchiseSeasonRankings called with seasonYear={SeasonYear}", 
                seasonYear);
            
            try
            {
                var url = $"franchise-season-rankings/seasonYear/{seasonYear}";
                _logger.LogDebug(
                    "Making HTTP GET request to Producer: {Url}", 
                    url);
                
                var response = await HttpClient.GetAsync(url);
                
                _logger.LogInformation(
                    "Received HTTP response from Producer, StatusCode={StatusCode}, seasonYear={SeasonYear}", 
                    response.StatusCode, 
                    seasonYear);
                
                response.EnsureSuccessStatusCode();
                
                var tmp = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug(
                    "Response body length: {Length} characters for seasonYear={SeasonYear}", 
                    tmp?.Length ?? 0, 
                    seasonYear);
                
                if (string.IsNullOrEmpty(tmp))
                {
                    _logger.LogWarning(
                        "Empty response body received from Producer for seasonYear={SeasonYear}", 
                        seasonYear);
                    return [];
                }
                
                var metrics = tmp.FromJson<List<FranchiseSeasonPollDto>>();
                
                _logger.LogInformation(
                    "Successfully deserialized {Count} polls for seasonYear={SeasonYear}", 
                    metrics?.Count ?? 0, 
                    seasonYear);
                
                return metrics ?? [];
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(
                    httpEx, 
                    "HTTP request failed in ProducerClient.GetFranchiseSeasonRankings for seasonYear={SeasonYear}, StatusCode={StatusCode}", 
                    seasonYear,
                    httpEx.StatusCode);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error in ProducerClient.GetFranchiseSeasonRankings for seasonYear={SeasonYear}", 
                    seasonYear);
                throw;
            }
        }
    }
}
