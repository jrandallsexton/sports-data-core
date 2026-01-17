using Microsoft.Extensions.Logging;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Season
{
    public interface IProvideSeasons : IProvideHealthChecks
    {
        Task<List<SeasonWeekDto>> GetCompletedSeasonWeeks(int seasonYear);

        Task<SeasonWeekDto?> GetCurrentSeasonWeek();

        Task<List<SeasonWeekDto>> GetCurrentAndLastWeekSeasonWeeks();
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

        public async Task<List<SeasonWeekDto>> GetCompletedSeasonWeeks(int seasonYear)
        {
            //var sql = _queryProvider.GetCompletedSeasonWeeks();
        }

        public async Task<SeasonWeekDto?> GetCurrentSeasonWeek()
        {
            //var sql = _queryProvider.GetCurrentSeasonWeek();

        }

        public async Task<List<SeasonWeekDto>> GetCurrentAndLastWeekSeasonWeeks()
        {
            // var sql = _queryProvider.GetCurrentAndLastWeekSeasonWeeks();
        }
    }
}
