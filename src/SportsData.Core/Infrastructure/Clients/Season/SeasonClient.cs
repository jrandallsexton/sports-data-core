using Microsoft.Extensions.Logging;

using SportsData.Core.Middleware.Health;

using System.Net.Http;

namespace SportsData.Core.Infrastructure.Clients.Season
{
    public interface IProvideSeasons : IProvideHealthChecks
    {

    }

    public class SeasonClient : ClientBase, IProvideSeasons
    {
        private readonly ILogger<SeasonClient> _logger;

        public SeasonClient(
            ILogger<SeasonClient> logger,
            HttpClient httpClient) :
            base(httpClient)
        {
            _logger = logger;
        }

        // TODO: Expose rankings
        // eg: /seasons/{seasonYear}/weeks/{{weekNumber}/rankings
        // eg: /seasons/{seasonYear}/weeks/{{weekNumber}/rankings?pollId={pollId}
        // eg: /seasons/{seasonYear}/weeks/{{weekNumber}/rankings?pollId={pollId}&topN={topN}
    }
}
