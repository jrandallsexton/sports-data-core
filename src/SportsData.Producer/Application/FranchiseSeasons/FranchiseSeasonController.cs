using Microsoft.AspNetCore.Mvc;

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
        [Route("seasonYear/{seasonYear}/metrics")]
        public async Task<IActionResult> GetFranchiseSeasonMetricsBySeasonYear(int seasonYear)
        {
            var metrics = await _franchiseSeasonMetricsService.GetFranchiseSeasonMetricsBySeasonYear(seasonYear);
            return Ok(metrics);
        }

        [HttpGet]
        [Route("id/{franchiseSeasonId}/metrics")]
        public async Task<IActionResult> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId)
        {
            var metrics = await _franchiseSeasonMetricsService.GetFranchiseSeasonMetricsByFranchiseSeasonId(franchiseSeasonId);
            return Ok(metrics);
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
