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

        public FranchiseSeasonRankingService(TeamSportDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<Result<List<FranchiseSeasonPollDto>>> GetCurrentPolls(int seasonYear)
        {
            var pollsToLoad = new List<string>(){"cfp", "ap", "usa"};
            var polls = new List<FranchiseSeasonPollDto>();

            foreach (var pollId in pollsToLoad)
            {
                var pollResult = await GetFranchiseSeasonPoll(pollId, seasonYear);

                if (pollResult.IsSuccess)
                {
                    polls.Add(pollResult.Value);
                }
                else
                {
                    // TODO: Logging
                }
            }

            return new Success<List<FranchiseSeasonPollDto>>(polls);
        }

        private async Task<Result<FranchiseSeasonPollDto>> GetFranchiseSeasonPoll(string pollId, int seasonYear)
        {
            var mostRecentApPollSeasonWeek = await _dataContext.FranchiseSeasonRankings
                .Where(x => x.SeasonYear == seasonYear && x.Type == pollId)
                .OrderByDescending(x => x.Date)
                .Select(x => new { x.SeasonWeekId, x.Date, x.ShortHeadline })
                .FirstOrDefaultAsync();

            if (mostRecentApPollSeasonWeek is null)
            {
                return new Failure<FranchiseSeasonPollDto>(
                    default!,
                    ResultStatus.NotFound,
                    []);
            }

            var seasonWeekNumber = await _dataContext.SeasonWeeks
                .Where(x => x.Id == mostRecentApPollSeasonWeek!.SeasonWeekId)
                .Select(x => x.Number)
                .FirstOrDefaultAsync();

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

            return new Success<FranchiseSeasonPollDto>(dto);
        }
    }
}
