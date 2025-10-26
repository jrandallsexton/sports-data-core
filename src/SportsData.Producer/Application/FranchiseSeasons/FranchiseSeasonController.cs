using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions;

namespace SportsData.Producer.Application.FranchiseSeasons
{  
    public class FranchiseSeasonController : ControllerBase
    {

        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FranchiseSeasonController(
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpPost]
        [Route("metrics/generate")]
        public IActionResult RefreshCompetitionMetrics()
        {
            var cmd = new GenerateFranchiseSeasonMetricsCommand()
            {
                CorrelationId = Guid.NewGuid(),
                SeasonYear = 2025, // TODO: remove hard-coding
                Sport = Core.Common.Sport.FootballNcaa // TODO: remove hard-coding
            };

            _backgroundJobProvider.Enqueue<IFranchiseSeasonMetricsService>(p => p.GenerateFranchiseSeasonMetrics(cmd));

            return Accepted(new { Message = $"FranchiseSeason metric generation initiated." });
        }
    }
}
