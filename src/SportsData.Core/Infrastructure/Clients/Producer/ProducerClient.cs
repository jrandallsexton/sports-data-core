using Microsoft.Extensions.Logging;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {
        Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear, CancellationToken cancellationToken = default);

        Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId, CancellationToken cancellationToken = default);

        Task RefreshContestMediaByContestId(Guid contestId, CancellationToken cancellationToken = default);

        Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear, CancellationToken cancellationToken = default);
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

        public async Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetrics(int seasonYear, CancellationToken cancellationToken = default)
        {
            return await GetOrDefaultAsync(
                $"franchise-seasons/seasonYear/{seasonYear}/metrics",
                new List<FranchiseSeasonMetricsDto>(),
                cancellationToken);
        }

        public async Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId, CancellationToken cancellationToken = default)
        {
            return await GetOrDefaultAsync(
                $"franchise-seasons/id/{franchiseSeasonId}/metrics",
                new FranchiseSeasonMetricsDto(),
                cancellationToken);
        }

        public async Task RefreshContestMediaByContestId(Guid contestId, CancellationToken cancellationToken = default)
        {
            var content = new StringContent(contestId.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"contests/{contestId}/media/refresh", content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<FranchiseSeasonPollDto>> GetFranchiseSeasonRankings(int seasonYear, CancellationToken cancellationToken = default)
        {
            return await GetOrDefaultAsync(
                $"franchise-season-rankings/seasonYear/{seasonYear}",
                new List<FranchiseSeasonPollDto>(),
                cancellationToken);
        }
    }
}
