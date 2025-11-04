
using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Contest
{
    [ApiController]
    [Route("ui/contest")]
    public class ContestController : ControllerBase
    {
        private readonly IContestService _contestService;

        public ContestController(IContestService contestService)
        {
            _contestService = contestService;
        }

        [HttpGet("{id}/overview")]
        public async Task<ActionResult<ContestOverviewDto>> GetContestById([FromRoute] Guid id)
        {
            var result = await _contestService.GetContestOverviewByContestId(id);
            return result.ToActionResult();
        }

        [HttpPost("{id}/refresh")]
        public async Task<ActionResult<ContestOverviewDto>> RefreshContestById([FromRoute] Guid id)
        {
            await _contestService.RefreshContestByContestId(id);
            return Accepted(id);
        }

        [HttpPost("{id}/media/refresh")]
        public async Task<ActionResult> RefreshContestMediaById([FromRoute] Guid id)
        {
            await _contestService.RefreshContestMediaByContestId(id);
            return Accepted(id);
        }
    }
}
