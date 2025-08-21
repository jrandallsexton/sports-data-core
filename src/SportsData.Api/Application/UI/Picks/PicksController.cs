using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Extensions;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks
{
    [ApiController]
    [Route("ui/picks")]
    public class PicksController : ApiControllerBase
    {
        private readonly IPickService _userPickService;

        public PicksController(IPickService userPickService)
        {
            _userPickService = userPickService;
        }

        [HttpGet("{sport}/{season}/{week}")]
        [Authorize]
        public async Task<IActionResult> GetPicksForWeek(
            [FromQuery] Sport sport,
            [FromQuery] int seasonYear,
            [FromQuery] int weekNumber)
        {
            var userId = HttpContext.GetCurrentUserId();
            await Task.Delay(100);
            return Ok();
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitPick([FromBody] SubmitUserPickRequest request, CancellationToken cancellationToken)
        {
            var userId = HttpContext.GetCurrentUserId();

            await _userPickService.SubmitPickAsync(userId, request, cancellationToken);

            return NoContent();
        }

        [HttpGet("{groupId}/week/{week}")]
        public async Task<ActionResult<List<UserPickDto>>> GetUserPicksByGroupAndWeek(
            Guid groupId,
            int week,
            CancellationToken cancellationToken)
        {
            var userId = HttpContext.GetCurrentUserId();

            var picks = await _userPickService.GetUserPicksByGroupAndWeek(userId, groupId, week, cancellationToken);

            return Ok(picks);
        }
    }
}
