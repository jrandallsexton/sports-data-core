using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

using SportsData.Api.Application.UI.Rankings.Dtos;
using SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Rankings;

[ApiController]
[Route("ui/rankings")]
[Authorize]
public class RankingsController : ApiControllerBase
{
    private readonly ILogger<RankingsController> _logger;

    public RankingsController(ILogger<RankingsController> logger)
    {
        _logger = logger;
    }

    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    [OutputCache(Duration = 6000)]
    [HttpGet("{seasonYear}")]
    public async Task<ActionResult<List<RankingsByPollIdByWeekDto>>> GetPolls(
        [FromRoute] int seasonYear,
        [FromServices] IGetRankingsBySeasonYearQueryHandler handler,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("GetPolls called with seasonYear={SeasonYear}", seasonYear);

        try
        {
            var result = await handler.ExecuteAsync(
                new GetRankingsBySeasonYearQuery { SeasonYear = seasonYear },
                cancellationToken);

            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "GetPolls succeeded for seasonYear={SeasonYear}, returned {Count} polls",
                    seasonYear,
                    result.Value.Count);
            }
            else
            {
                _logger.LogWarning(
                    "GetPolls failed for seasonYear={SeasonYear}, Status={Status}",
                    seasonYear,
                    result.Status);
            }

            return result.ToActionResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception in GetPolls for seasonYear={SeasonYear}",
                seasonYear);
            throw;
        }
    }

    [HttpGet("{seasonYear}/week/{week}")]
    public async Task<ActionResult<List<RankingsByPollIdByWeekDto>>> GetPollsBySeasonYearWeek(
        [FromRoute] int seasonYear,
        [FromRoute] int week,
        [FromServices] IGetPollRankingsByWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new GetPollRankingsByWeekQuery
            {
                SeasonYear = seasonYear,
                Week = week
            },
            cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{seasonYear}/week/{week}/poll/{poll}")]
    public async Task<ActionResult<RankingsByPollIdByWeekDto>> GetRankings(
        [FromRoute] int seasonYear,
        [FromRoute] int week,
        [FromRoute] string poll,
        [FromServices] IGetRankingsByPollWeekQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(
            new GetRankingsByPollWeekQuery
            {
                SeasonYear = seasonYear,
                Week = week,
                Poll = poll
            },
            cancellationToken);

        return result.ToActionResult();
    }
}
