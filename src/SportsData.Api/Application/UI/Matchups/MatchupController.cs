using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.NewFolder
{
    [ApiController]
    [Route("ui/matchup")]
    public class MatchupController : ApiControllerBase
    {
        private readonly AppDataContext _dbContext;

        public MatchupController(AppDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("{id}/preview")]
        [Authorize]
        public async Task<IActionResult> GetPreviewById(Guid id)
        {
            var preview = await _dbContext.MatchupPreviews.FirstOrDefaultAsync(x => x.ContestId == id);

            if (preview is null)
                return NotFound();

            return Ok(new MatchupPreviewDto()
            {
                Overview = preview.Overview,
                Analysis = preview.Analysis,
                Prediction = preview.Prediction
            });
        }
    }
}
