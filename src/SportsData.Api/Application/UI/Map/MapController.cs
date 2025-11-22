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
        public async Task<ActionResult<Matchup>> GetMatchups()
        {
            var result = await _mapService.GetMatchupsForCurrentWeek();
            return Ok(result);
        }
    }
}
