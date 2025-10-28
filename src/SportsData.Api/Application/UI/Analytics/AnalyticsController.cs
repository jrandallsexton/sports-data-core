using Microsoft.AspNetCore.Mvc;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.UI.Analytics
{
    [ApiController]
    [Route("ui/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public AnalyticsController(IProvideCanonicalData canonicalDataProvider)
        {
            _canonicalDataProvider = canonicalDataProvider;
        }

        [HttpGet("franchise-season/{seasonYear}")]
        public async Task<ActionResult<List<FranchiseSeasonMetricsDto>>> GetFranchiseSeasonMetrics([FromRoute] int seasonYear)
        {
            var data = await _canonicalDataProvider.GetFranchiseSeasonMetricsBySeasonYear(seasonYear);
            return Ok(data);
        }
    }
}
