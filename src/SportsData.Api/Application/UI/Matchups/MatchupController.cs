using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Matchups
{
    [ApiController]
    [Route("ui/matchup")]
    public class MatchupController : ApiControllerBase
    {
        private readonly AppDataContext _dbContext;
        private readonly IProvideCanonicalData _canonicalData;

        public MatchupController(
            AppDataContext dbContext,
            IProvideCanonicalData canonicalData)
        {
            _dbContext = dbContext;
            _canonicalData = canonicalData;
        }

        [HttpGet("{id}/preview")]
        [Authorize]
        public async Task<IActionResult> GetPreviewById(Guid id)
        {
            // TODO: Clean up this mess

            var preview = await _dbContext.MatchupPreviews
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedUtc)
                .FirstOrDefaultAsync(x => x.ContestId == id && x.RejectedUtc == null);

            if (preview is null)
                return NotFound();

            var matchup = await _dbContext.PickemGroupMatchups
                .Where(m => m.ContestId == id)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            var canonical = await _canonicalData.GetMatchupForPreview(id);

            var suWinner = canonical.AwayFranchiseSeasonId == preview.PredictedStraightUpWinner
                ? canonical.Away
                : canonical.Home;

            var atsWinner = canonical.AwayFranchiseSeasonId == preview.PredictedSpreadWinner
                ? canonical.Away
                : canonical.Home;

            var implied = (canonical is { HomeSpread: not null, OverUnder: not null })
                ? VegasScoreHelper.CalculateImpliedScore(canonical.HomeSpread.Value, canonical.OverUnder.Value)
                : string.Empty;

            return Ok(new MatchupPreviewDto()
            {
                Id = preview.Id,
                ContestId = preview.ContestId,
                Overview = preview.Overview,
                Analysis = preview.Analysis,
                Prediction = preview.Prediction,
                StraightUpWinner = suWinner,
                AtsWinner = atsWinner,
                AwayScore = preview.AwayScore,
                HomeScore = preview.HomeScore,
                VegasImpliedScore = implied,
                GeneratedUtc = preview.CreatedUtc
            });
        }
    }
}
