using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetPollBySeasonWeekId;

public interface IGetPollBySeasonWeekIdQueryHandler
{
    Task<Result<FranchiseSeasonPollDto>> ExecuteAsync(
        GetPollBySeasonWeekIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetPollBySeasonWeekIdQueryHandler : IGetPollBySeasonWeekIdQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetPollBySeasonWeekIdQueryHandler> _logger;

    public GetPollBySeasonWeekIdQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetPollBySeasonWeekIdQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<FranchiseSeasonPollDto>> ExecuteAsync(
        GetPollBySeasonWeekIdQuery query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read from the SeasonPoll* store — the store the weekly rankings
            // sourcing job feeds directly. The FranchiseSeasonRanking store
            // this used to read only fills when TeamSeason docs are re-sourced
            // with a TeamSeasonRank inclusion filter, so it silently went
            // stale between backfills (2025's final AP poll never landed
            // there).
            var week = await _dataContext.SeasonPollWeeks
                .AsNoTracking()
                .Where(x => x.SeasonWeekId == query.SeasonWeekId && x.SeasonPoll.Slug == query.PollSlug)
                .Select(x => new
                {
                    x.Id,
                    x.ShortHeadline,
                    x.DateUtc,
                    x.SeasonPoll.SeasonYear
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (week is null)
            {
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("pollSlug", $"No rankings found for poll '{query.PollSlug}' in season week {query.SeasonWeekId}")]);
            }

            var pollEntries = await _dataContext.SeasonPollWeekEntries
                .AsNoTracking()
                .Where(x => x.SeasonPollWeekId == week.Id &&
                            !x.IsOtherReceivingVotes &&
                            !x.IsDroppedOut)
                .OrderBy(x => x.Current)
                .Select(x => new FranchiseSeasonPollDto.FranchiseSeasonPollEntryDto
                {
                    FranchiseLogoUrl = x.FranchiseSeason.Logos
                        .Select(l => l.Uri.OriginalString)
                        .FirstOrDefault() ?? string.Empty,
                    FranchiseName = x.FranchiseSeason.DisplayNameShort,
                    FranchiseSlug = x.FranchiseSeason.Slug,
                    Rank = x.Current,
                    FirstPlaceVotes = x.FirstPlaceVotes,
                    FranchiseSeasonId = x.FranchiseSeasonId,
                    Points = (int)x.Points,
                    // Empty string / zero are the entry's "absent" sentinels;
                    // the DTO contract uses null (HasTrends keys on it).
                    Trend = x.Trend == "" ? null : x.Trend,
                    PreviousRank = x.Previous == 0 ? null : x.Previous,
                    // Preseason entries carry NULL records; fall back to the
                    // FranchiseSeason totals like the Dapper queries do.
                    Losses = x.Losses ?? x.FranchiseSeason.Losses,
                    Wins = x.Wins ?? x.FranchiseSeason.Wins
                })
                .ToListAsync(cancellationToken);

            if (pollEntries.Count == 0)
            {
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("pollSlug", $"No rankings found for poll '{query.PollSlug}' in season week {query.SeasonWeekId}")]);
            }

            var seasonWeekNumber = await _dataContext.SeasonWeeks
                .AsNoTracking()
                .Where(x => x.Id == query.SeasonWeekId)
                .Select(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken);

            var dto = new FranchiseSeasonPollDto
            {
                Entries = pollEntries,
                PollId = query.PollSlug,
                PollName = week.ShortHeadline,
                SeasonYear = week.SeasonYear,
                Week = seasonWeekNumber,
                HasFirstPlaceVotes = pollEntries.Sum(x => x.FirstPlaceVotes) > 0,
                HasPoints = pollEntries.Sum(x => x.Points) > 0,
                HasTrends = pollEntries.Any(x => x.Trend != null),
                PollDateUtc = week.DateUtc ?? DateTime.MinValue
            };

            return new Success<FranchiseSeasonPollDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in GetPollBySeasonWeekId. SeasonWeekId={SeasonWeekId}, PollSlug={PollSlug}",
                query.SeasonWeekId, query.PollSlug);

            return new Failure<FranchiseSeasonPollDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("Error", "An unexpected error occurred while retrieving rankings")]);
        }
    }
}
