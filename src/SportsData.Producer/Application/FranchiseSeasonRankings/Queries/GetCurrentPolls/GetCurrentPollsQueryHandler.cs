using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;

public interface IGetCurrentPollsQueryHandler
{
    Task<Result<List<FranchiseSeasonPollDto>>> ExecuteAsync(
        GetCurrentPollsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCurrentPollsQueryHandler : IGetCurrentPollsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetCurrentPollsQueryHandler> _logger;

    public GetCurrentPollsQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetCurrentPollsQueryHandler> logger)
    {
        _dataContext = dataContext;
        _logger = logger;
    }

    public async Task<Result<List<FranchiseSeasonPollDto>>> ExecuteAsync(
        GetCurrentPollsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetCurrentPolls started. SeasonYear={SeasonYear}", 
            query.SeasonYear);
        
        try
        {
            var pollsToLoad = new List<string> { "cfp", "ap", "usa" };
            var polls = new List<FranchiseSeasonPollDto>();

            foreach (var pollId in pollsToLoad)
            {
                _logger.LogDebug(
                    "Loading poll. PollId={PollId}, SeasonYear={SeasonYear}", 
                    pollId, 
                    query.SeasonYear);
                
                var pollResult = await GetFranchiseSeasonPoll(pollId, query.SeasonYear, cancellationToken);

                if (pollResult.IsSuccess)
                {
                    _logger.LogInformation(
                        "Poll loaded successfully. PollId={PollId}, EntryCount={EntryCount}, SeasonYear={SeasonYear}", 
                        pollId, 
                        pollResult.Value.Entries.Count, 
                        query.SeasonYear);
                    polls.Add(pollResult.Value);
                }
                else
                {
                    _logger.LogWarning(
                        "Poll load failed. PollId={PollId}, SeasonYear={SeasonYear}, Status={Status}", 
                        pollId, 
                        query.SeasonYear, 
                        pollResult.Status);
                }
            }

            if (polls.Count == 0)
            {
                _logger.LogWarning(
                    "No polls found. SeasonYear={SeasonYear}", 
                    query.SeasonYear);
                
                return new Failure<List<FranchiseSeasonPollDto>>(
                    new List<FranchiseSeasonPollDto>(),
                    ResultStatus.NotFound,
                    [new ValidationFailure("seasonYear", $"No polls found for season year {query.SeasonYear}")]);
            }

            _logger.LogInformation(
                "GetCurrentPolls completed successfully. SeasonYear={SeasonYear}, PollCount={PollCount}", 
                query.SeasonYear, 
                polls.Count);

            return new Success<List<FranchiseSeasonPollDto>>(polls);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Error in GetCurrentPolls. SeasonYear={SeasonYear}", 
                query.SeasonYear);
            
            return new Failure<List<FranchiseSeasonPollDto>>(
                new List<FranchiseSeasonPollDto>(),
                ResultStatus.Error,
                [new ValidationFailure("Error", ex.Message)]);
        }
    }

    private async Task<Result<FranchiseSeasonPollDto>> GetFranchiseSeasonPoll(
        string pollId, 
        int seasonYear, 
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "GetFranchiseSeasonPoll started. PollId={PollId}, SeasonYear={SeasonYear}", 
            pollId, 
            seasonYear);
        
        try
        {
            var mostRecentPoll = await _dataContext.FranchiseSeasonRankings
                .Where(x => x.SeasonYear == seasonYear && x.Type == pollId)
                .OrderByDescending(x => x.Date)
                .Select(x => new { x.SeasonWeekId, x.Date, x.ShortHeadline })
                .FirstOrDefaultAsync(cancellationToken);

            if (mostRecentPoll is null)
            {
                _logger.LogWarning(
                    "No rankings found. PollId={PollId}, SeasonYear={SeasonYear}", 
                    pollId, 
                    seasonYear);
                
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("pollId", $"No rankings found for poll '{pollId}' in season {seasonYear}")]);
            }

            _logger.LogInformation(
                "Most recent poll found. PollId={PollId}, SeasonYear={SeasonYear}, SeasonWeekId={SeasonWeekId}, Date={Date}", 
                pollId, 
                seasonYear, 
                mostRecentPoll.SeasonWeekId, 
                mostRecentPoll.Date);

            var seasonWeekNumber = await _dataContext.SeasonWeeks
                .Where(x => x.Id == mostRecentPoll.SeasonWeekId)
                .Select(x => x.Number)
                .FirstOrDefaultAsync(cancellationToken);

            _logger.LogDebug(
                "Retrieved season week. WeekNumber={WeekNumber}, PollId={PollId}, SeasonYear={SeasonYear}", 
                seasonWeekNumber, 
                pollId, 
                seasonYear);

            var pollEntries = await _dataContext.FranchiseSeasonRankings
                .Where(x => x.SeasonWeekId == mostRecentPoll.SeasonWeekId && x.Type == pollId)
                .OrderBy(x => x.Rank.Current)
                .Select(x => new FranchiseSeasonPollDto.FranchiseSeasonPollEntryDto()
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
                _logger.LogWarning(
                    "No poll entries found. PollId={PollId}, SeasonYear={SeasonYear}, SeasonWeekId={SeasonWeekId}", 
                    pollId, 
                    seasonYear, 
                    mostRecentPoll.SeasonWeekId);
                
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("pollId", $"No entries found for poll '{pollId}' in season {seasonYear}")]);
            }

            _logger.LogInformation(
                "Poll entries retrieved successfully. PollId={PollId}, SeasonYear={SeasonYear}, EntryCount={EntryCount}", 
                pollId, 
                seasonYear, 
                pollEntries.Count);

            var dto = new FranchiseSeasonPollDto()
            {
                Entries = pollEntries,
                PollId = pollId,
                PollName = mostRecentPoll.ShortHeadline,
                SeasonYear = seasonYear,
                Week = seasonWeekNumber,
                HasFirstPlaceVotes = pollEntries.Sum(x => x.FirstPlaceVotes) > 0,
                HasPoints = pollEntries.Sum(x => x.Points) > 0,
                HasTrends = pollEntries.Any(x => x.Trend != null),
                PollDateUtc = mostRecentPoll.Date!.Value
            };

            _logger.LogInformation(
                "Poll DTO created successfully. PollId={PollId}, SeasonYear={SeasonYear}, EntryCount={EntryCount}", 
                pollId, 
                seasonYear, 
                dto.Entries.Count);

            return new Success<FranchiseSeasonPollDto>(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, 
                "Error in GetFranchiseSeasonPoll. PollId={PollId}, SeasonYear={SeasonYear}", 
                pollId, 
                seasonYear);
            
            return new Failure<FranchiseSeasonPollDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("Error", ex.Message)]);
        }
    }
}
