using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Map
{
    [ApiController]
    [Route("ui/map")]
    public class MapController : ApiControllerBase
    {
        private readonly IMapService _mapService;

        public MapController(IMapService mapService)
        {
            _mapService = mapService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<Matchup>> GetMatchups(
            [FromQuery] Guid? leagueId,
            [FromQuery] int? weekNumber)
        {
            var query = new GetMapMatchupsQuery
            {
                LeagueId = leagueId,
                WeekNumber = weekNumber
            };

            var result = await _mapService.GetMatchups(query);

            return Ok(result);
        }
    }
}
