using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Middleware.Health;

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace SportsData.Core.Infrastructure.Clients.Producer
{
    public interface IProvideProducers : IProvideHealthChecks
    {
        Task<VenueDto?> GetVenue(string id);
        Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);
        Task RefreshContestByContestId(Guid contestId);
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

        public async Task<VenueDto?> GetVenue(string id)
        {
            var response = await HttpClient.GetAsync($"venue/{id}");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var venue = tmp.FromJson<Success<VenueDto>>();

            return venue?.Value;
        }

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId)
        {
            var response = await HttpClient.GetAsync($"contest/{contestId}/overview");
            response.EnsureSuccessStatusCode();
            var tmp = await response.Content.ReadAsStringAsync();
            var overview = tmp.FromJson<ContestOverviewDto>();

            return overview ?? new ContestOverviewDto();
        }

        public async Task RefreshContestByContestId(Guid contestId)
        {
            var content = new StringContent(contestId.ToJson(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync($"contest/{contestId}/update", content);
            response.EnsureSuccessStatusCode();
        }
    }
}
