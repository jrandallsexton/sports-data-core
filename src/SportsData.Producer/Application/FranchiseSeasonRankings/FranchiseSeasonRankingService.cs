using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasonRankings
{
    public interface IFranchiseSeasonRankingService
    {
        Task<Result<List<FranchiseSeasonPollDto>>> GetCurrentPolls(int seasonYear);
    }

    public class FranchiseSeasonRankingService : IFranchiseSeasonRankingService
    {
        private readonly TeamSportDataContext _dataContext;
        private readonly ILogger<FranchiseSeasonRankingService> _logger;

        public FranchiseSeasonRankingService(
            TeamSportDataContext dataContext,
            ILogger<FranchiseSeasonRankingService> logger)
        {
            _dataContext = dataContext;
            _logger = logger;
        }

        public async Task<Result<List<FranchiseSeasonPollDto>>> GetCurrentPolls(int seasonYear)
        {
            _logger.LogInformation(
                "FranchiseSeasonRankingService.GetCurrentPolls called with seasonYear={SeasonYear}", 
                seasonYear);
            
            try
            {
                var pollsToLoad = new List<string> {"cfp", "ap", "usa"};
                var polls = new List<FranchiseSeasonPollDto>();

                foreach (var pollId in pollsToLoad)
                {
                    _logger.LogDebug(
                        "Loading poll {PollId} for seasonYear={SeasonYear}", 
                        pollId, 
                        seasonYear);
                    
                    var pollResult = await GetFranchiseSeasonPoll(pollId, seasonYear);

                    if (pollResult.IsSuccess)
                    {
                        _logger.LogInformation(
                            "Successfully loaded poll {PollId} with {EntryCount} entries for seasonYear={SeasonYear}", 
                            pollId, 
                            pollResult.Value.Entries.Count, 
                            seasonYear);
                        polls.Add(pollResult.Value);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to load poll {PollId} for seasonYear={SeasonYear}, Status={Status}", 
                            pollId, 
                            seasonYear, 
                            pollResult.Status);
                    }
                }

                _logger.LogInformation(
                    "GetCurrentPolls completed for seasonYear={SeasonYear}, returning {Count} polls", 
                    seasonYear, 
                    polls.Count);

                return new Success<List<FranchiseSeasonPollDto>>(polls);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error in GetCurrentPolls for seasonYear={SeasonYear}", 
                    seasonYear);
                throw;
            }
        }

        private async Task<Result<FranchiseSeasonPollDto>> GetFranchiseSeasonPoll(string pollId, int seasonYear)
        {
            _logger.LogDebug(
                "GetFranchiseSeasonPoll called with pollId={PollId}, seasonYear={SeasonYear}", 
                pollId, 
                seasonYear);
            
            try
            {
                _logger.LogDebug(
                    "Querying database for most recent poll: pollId={PollId}, seasonYear={SeasonYear}", 
                    pollId, 
                    seasonYear);
                
                var mostRecentApPollSeasonWeek = await _dataContext.FranchiseSeasonRankings
                    .Where(x => x.SeasonYear == seasonYear && x.Type == pollId)
                    .OrderByDescending(x => x.Date)
                    .Select(x => new { x.SeasonWeekId, x.Date, x.ShortHeadline })
                    .FirstOrDefaultAsync();

                if (mostRecentApPollSeasonWeek is null)
                {
                    _logger.LogWarning(
                        "No rankings found for pollId={PollId}, seasonYear={SeasonYear}", 
                        pollId, 
                        seasonYear);
                    
                    return new Failure<FranchiseSeasonPollDto>(
                        default!,
                        ResultStatus.NotFound,
                        []);
                }

                _logger.LogInformation(
                    "Found most recent poll: pollId={PollId}, seasonYear={SeasonYear}, SeasonWeekId={SeasonWeekId}, Date={Date}", 
                    pollId, 
                    seasonYear, 
                    mostRecentApPollSeasonWeek.SeasonWeekId, 
                    mostRecentApPollSeasonWeek.Date);

                var seasonWeekNumber = await _dataContext.SeasonWeeks
                    .Where(x => x.Id == mostRecentApPollSeasonWeek!.SeasonWeekId)
                    .Select(x => x.Number)
                    .FirstOrDefaultAsync();

                _logger.LogDebug(
                    "SeasonWeekNumber={WeekNumber} for pollId={PollId}, seasonYear={SeasonYear}", 
                    seasonWeekNumber, 
                    pollId, 
                    seasonYear);

                _logger.LogDebug(
                    "Querying poll entries for pollId={PollId}, seasonYear={SeasonYear}, SeasonWeekId={SeasonWeekId}", 
                    pollId, 
                    seasonYear, 
                    mostRecentApPollSeasonWeek.SeasonWeekId);
                
                var pollEntries = await _dataContext.FranchiseSeasonRankings
                    .Include(x => x.Rank)
                    .Include(x => x.FranchiseSeason)
                    .ThenInclude(x => x.Franchise)
                    .Include(x => x.FranchiseSeason)
                    .ThenInclude(x => x.Logos)
                    .Where(x => x.SeasonWeekId == mostRecentApPollSeasonWeek!.SeasonWeekId && x.Type == pollId)
                    .OrderBy(x => x.Rank.Current)
                    .Select(x => new FranchiseSeasonPollDto.FranchiseSeasonPollEntryDto()
                    {
                        FranchiseLogoUrl = x.FranchiseSeason.Logos.ToList().First().Uri.OriginalString,
                        FranchiseName = x.Franchise.DisplayNameShort,
                        FranchiseSlug = x.Franchise.Slug,
                        Rank = x.Rank.Current,
                        FirstPlaceVotes = x.Rank.FirstPlaceVotes,
                        FranchiseSeasonId = x.FranchiseSeasonId,
                        Points = int.Parse(x.Rank.Points.ToString()),
                        Trend = x.Rank.Trend,
                        Losses = x.FranchiseSeason.Losses,
                        PreviousRank = x.Rank.Previous,
                        Wins = x.FranchiseSeason.Wins
                    })
                    .ToListAsync();

                _logger.LogInformation(
                    "Retrieved {Count} poll entries for pollId={PollId}, seasonYear={SeasonYear}", 
                    pollEntries.Count, 
                    pollId, 
                    seasonYear);

                var dto = new FranchiseSeasonPollDto()
                {
                    Entries = pollEntries,
                    PollId = pollId,
                    PollName = mostRecentApPollSeasonWeek!.ShortHeadline,
                    SeasonYear = seasonYear,
                    Week = seasonWeekNumber,
                    HasFirstPlaceVotes = pollEntries.Sum(x => x.FirstPlaceVotes) > 0,
                    HasPoints = pollEntries.Sum(x => x.Points) > 0,
                    HasTrends = pollEntries.Any(x => x.Trend != null),
                    PollDateUtc = mostRecentApPollSeasonWeek!.Date!.Value
                };

                _logger.LogInformation(
                    "Successfully created poll DTO for pollId={PollId}, seasonYear={SeasonYear}, EntryCount={EntryCount}", 
                    pollId, 
                    seasonYear, 
                    dto.Entries.Count);

                return new Success<FranchiseSeasonPollDto>(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, 
                    "Error in GetFranchiseSeasonPoll for pollId={PollId}, seasonYear={SeasonYear}", 
                    pollId, 
                    seasonYear);
                throw;
            }
        }
    }
}
