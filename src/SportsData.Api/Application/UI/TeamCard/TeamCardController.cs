using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.TeamCard.Dtos;
using SportsData.Api.Application.UI.TeamCard.Queries;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.TeamCard
{
    [ApiController]
    [Route("ui/teamcard/sport/{sport}/league/{league}/team/{slug}/{seasonYear}")]
    public class TeamCardController : ApiControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetTeamCard(
            string sport,
            string league,
            string slug,
            int seasonYear,
            [FromServices] ITeamCardService service,
            CancellationToken cancellationToken)
        {
            var query = new GetTeamCardQuery
            {
                Sport = sport,
                League = league,
                Slug = slug,
                SeasonYear = seasonYear
            };

            var result = await service.GetTeamCard(query, cancellationToken);

            if (result.IsSuccess)
                return Ok(result.Value);

            if (result is Failure<TeamCardDto?> failure)
            {
                return result.Status switch
                {
                    ResultStatus.Validation => BadRequest(new { failure.Errors }),
                    ResultStatus.NotFound => NotFound(new { failure.Errors }),
                    ResultStatus.Unauthorized => Unauthorized(new { failure.Errors }),
                    ResultStatus.Forbid => Forbid(),
                    _ => StatusCode(500, new { failure.Errors })
                };
            }

            return StatusCode(500); // fallback safety
        }
    }
}