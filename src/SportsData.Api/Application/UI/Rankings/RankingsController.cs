using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

using SportsData.Api.Application.UI.Rankings.Queries.GetPollRankingsByWeek;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollSeasonWeekId;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsByPollWeek;
using SportsData.Api.Application.UI.Rankings.Queries.GetRankingsBySeasonYear;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
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

    [ResponseCache(Duration = 6000, Location = ResponseCacheLocation.Client)]
    [OutputCache(Duration = 6000)]
    [HttpGet("{seasonYear}")]
    public async Task<ActionResult<List<RankingsByPollIdByWeekDto>>> GetPolls(
        [FromRoute] int seasonYear,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetRankingsBySeasonYearQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetPolls called with seasonYear={SeasonYear}", seasonYear);

        try
        {
            var mode = ModeMapper.ResolveMode(sport, league);
            var result = await handler.ExecuteAsync(
                new GetRankingsBySeasonYearQuery { SeasonYear = seasonYear, Sport = mode },
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

    [HttpGet("by-week/{seasonWeekId}/poll/{poll}")]
    public async Task<ActionResult<RankingsByPollIdByWeekDto>> GetRankingsBySeasonWeekId(
        [FromRoute] Guid seasonWeekId,
        [FromRoute] string poll,
        [FromQuery] string sport = "football",
        [FromQuery] string league = "ncaa",
        [FromServices] IGetRankingsByPollSeasonWeekIdQueryHandler handler = default!,
        CancellationToken cancellationToken = default)
    {
        var mode = ModeMapper.ResolveMode(sport, league);
        var result = await handler.ExecuteAsync(
            new GetRankingsByPollSeasonWeekIdQuery
            {
                SeasonWeekId = seasonWeekId,
                Poll = poll,
                Sport = mode
            },
            cancellationToken);

        return result.ToActionResult();
    }
}
