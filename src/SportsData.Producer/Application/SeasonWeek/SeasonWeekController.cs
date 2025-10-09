using Microsoft.AspNetCore.Mvc;

namespace SportsData.Producer.Application.SeasonWeek
{
    [Route("api/seasonWeek")]
    [ApiController]
    public class SeasonWeekController : ControllerBase
    {
        private readonly ISeasonWeekService _seasonWeekService;

        public SeasonWeekController(ISeasonWeekService seasonWeekService)
        {
            _seasonWeekService = seasonWeekService;
        }

        [HttpPost]
        [Route("{seasonWeekId}/update")]
        public async Task<IActionResult> UpdateSeasonWeekContests([FromRoute] Guid seasonWeekId)
        {
            await _seasonWeekService.UpdateSeasonWeekContests(seasonWeekId);
            return Accepted(seasonWeekId);
        }
    }
}
