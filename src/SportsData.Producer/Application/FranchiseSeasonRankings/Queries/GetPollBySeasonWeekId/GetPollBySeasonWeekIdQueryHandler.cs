using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

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
            var pollEntries = await _dataContext.FranchiseSeasonRankings
                .Where(x => x.SeasonWeekId == query.SeasonWeekId && x.Type == query.PollSlug)
                .OrderBy(x => x.Rank.Current)
                .Select(x => new FranchiseSeasonPollDto.FranchiseSeasonPollEntryDto
                {
                    FranchiseLogoUrl = x.FranchiseSeason.Logos.FirstOrDefault() != null
                        ? x.FranchiseSeason.Logos.FirstOrDefault()!.Uri.OriginalString
                        : string.Empty,
                    FranchiseName = x.Franchise.DisplayNameShort,
                    FranchiseSlug = x.Franchise.Slug,
                    Rank = x.Rank.Current,
                    FirstPlaceVotes = x.Rank.FirstPlaceVotes,
                    FranchiseSeasonId = x.FranchiseSeasonId,
                    Points = (int)x.Rank.Points,
                    Trend = x.Rank.Trend,
                    Losses = x.FranchiseSeason.Losses,
                    PreviousRank = x.Rank.Previous,
                    Wins = x.FranchiseSeason.Wins
                })
                .ToListAsync(cancellationToken);

            if (pollEntries.Count == 0)
            {
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("pollSlug", $"No rankings found for poll '{query.PollSlug}' in season week {query.SeasonWeekId}")]);
            }

            var firstEntry = await _dataContext.FranchiseSeasonRankings
                .Where(x => x.SeasonWeekId == query.SeasonWeekId && x.Type == query.PollSlug)
                .Select(x => new { x.ShortHeadline, x.Date, x.SeasonYear })
                .FirstAsync(cancellationToken);

            var seasonWeekNumber = await _dataContext.SeasonWeeks
                .Where(x => x.Id == query.SeasonWeekId)
                .Select(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken);

            var dto = new FranchiseSeasonPollDto
            {
                Entries = pollEntries,
                PollId = query.PollSlug,
                PollName = firstEntry.ShortHeadline,
                SeasonYear = firstEntry.SeasonYear,
                Week = seasonWeekNumber,
                HasFirstPlaceVotes = pollEntries.Sum(x => x.FirstPlaceVotes) > 0,
                HasPoints = pollEntries.Sum(x => x.Points) > 0,
                HasTrends = pollEntries.Any(x => x.Trend != null),
                PollDateUtc = firstEntry.Date ?? DateTime.MinValue
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
                [new ValidationFailure("Error", ex.Message)]);
        }
    }
}
