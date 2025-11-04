using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Matchups
{
    [ApiController]
    [Route("ui/matchup")]
    public class MatchupController : ApiControllerBase
    {
        private readonly IMatchupService _matchupService;

        public MatchupController(IMatchupService matchupService)
        {
            _matchupService = matchupService;
        }

        [HttpGet("{id}/preview")]
        [Authorize]
        public async Task<ActionResult<MatchupPreviewDto>> GetPreviewById(Guid id)
        {
            var result = await _matchupService.GetPreviewById(id);
            return result.ToActionResult();
        }
    }
}
