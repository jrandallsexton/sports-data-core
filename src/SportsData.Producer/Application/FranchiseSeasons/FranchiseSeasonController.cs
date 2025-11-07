using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.FranchiseSeasons
{
    [Route("api/franchise-season")]
    public class FranchiseSeasonController : ControllerBase
    {
        private readonly IFranchiseSeasonMetricsService _franchiseSeasonMetricsService;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FranchiseSeasonController(
            IFranchiseSeasonMetricsService franchiseSeasonMetricsService,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _franchiseSeasonMetricsService = franchiseSeasonMetricsService;
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpGet]
        [Route("id/{franchiseSeasonId}/metrics")]
        public async Task<ActionResult<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId)
        {
            var result = await _franchiseSeasonMetricsService
                .GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId);

            return result.ToActionResult();
        }

        [HttpGet]
        [Route("seasonYear/{seasonYear}/metrics")]
        public async Task<ActionResult<List<FranchiseSeasonMetricsDto>>> GetFranchiseSeasonMetricsBySeasonYear([FromRoute] int seasonYear)
        {
            var result = await _franchiseSeasonMetricsService.GetFranchiseSeasonMetricsBySeasonYear(seasonYear);
            return result.ToActionResult();
        }

        [HttpPost]
        [Route("seasonYear/{seasonYear}/metrics/generate")]
        public IActionResult RefreshCompetitionMetrics([FromRoute] int seasonYear)
        {
            var cmd = new GenerateFranchiseSeasonMetricsCommand()
            {
                CorrelationId = Guid.NewGuid(),
                SeasonYear = seasonYear,
                Sport = Core.Common.Sport.FootballNcaa // TODO: remove hard-coding
            };

            _backgroundJobProvider.Enqueue<IFranchiseSeasonMetricsService>(p => p.GenerateFranchiseSeasonMetrics(cmd));

            return Accepted(cmd.CorrelationId);
        }
    }
}
