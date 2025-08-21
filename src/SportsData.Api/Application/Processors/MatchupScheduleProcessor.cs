using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Processors
{
    public interface IScheduleGroupWeekMatchups
    {
        Task Process(ScheduleGroupWeekMatchupsCommand command);
    }

    public class MatchupScheduleProcessor : IScheduleGroupWeekMatchups
    {
        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupScheduleProcessor> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;

        public MatchupScheduleProcessor(
            AppDataContext dataContext,
            ILogger<MatchupScheduleProcessor> logger,
            IProvideCanonicalData canonicalDataProvider)
        {
            _dataContext = dataContext;
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
        }

        public async Task Process(ScheduleGroupWeekMatchupsCommand command)
        {
            var group = await _dataContext.PickemGroups
                .Include(x => x.Conferences)
                .FirstOrDefaultAsync(x => x.Id == command.GroupId);

            if (group is null)
            {
                _logger.LogError("Group not found");
                return;
            }

            // at this point, we have a group - but we need to generate matchups for the specified week
            var groupWeek = await _dataContext.PickemGroupWeeks
                .Where(x => x.GroupId == command.GroupId && x.SeasonWeekId == command.SeasonWeekId)
                .FirstOrDefaultAsync();

            if (groupWeek is null)
            {
                groupWeek = new PickemGroupWeek()
                {
                    Id = Guid.NewGuid(),
                    AreMatchupsGenerated = false,
                    SeasonWeek = command.SeasonWeek,
                    SeasonWeekId = command.SeasonWeekId,
                    SeasonYear = command.SeasonYear,
                    GroupId = command.GroupId
                };
                await _dataContext.PickemGroupWeeks.AddAsync(groupWeek);
                await _dataContext.SaveChangesAsync();
            }
            else
            {
                if (groupWeek.AreMatchupsGenerated)
                {
                    _logger.LogWarning("Matchups already generated");
                    return;
                }
            }

            // proceed with getting this week's matchups

            // 1. How many AP Ranks to include?
            var topX = (int)(group.RankingFilter ?? 0);

            // 2. are there conferences to always be included?
            var conferenceSlugs = group.Conferences.Select(x => x.ConferenceSlug).ToList();

            var allMatchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();

            var groupMatchups = allMatchups
                .Where(x =>
                    (x.AwayRank.HasValue && x.AwayRank <= topX) ||
                    (x.HomeRank.HasValue && x.HomeRank <= topX) ||
                    (x.AwayConferenceSlug != null && conferenceSlugs.Contains(x.AwayConferenceSlug)) ||
                    (x.HomeConferenceSlug != null && conferenceSlugs.Contains(x.HomeConferenceSlug))
                );

            foreach (var groupMatchup in groupMatchups)
            {
                groupWeek.Matchups.Add(new PickemGroupMatchup()
                {
                    Id = Guid.NewGuid(),
                    AwaySpread = groupMatchup.AwaySpread,
                    ContestId = groupMatchup.ContestId,
                    CreatedBy = Guid.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    GroupId = group.Id,
                    GroupWeek = groupWeek,
                    HomeSpread = groupMatchup.HomeSpread,
                    OverOdds = groupMatchup.OverOdds,
                    OverUnder = groupMatchup.OverUnder,
                    SeasonWeek = command.SeasonWeek,
                    SeasonWeekId = groupMatchup.SeasonWeekId,
                    SeasonYear = command.SeasonYear,
                    Spread = groupMatchup.Spread,
                    UnderOdds = groupMatchup.UnderOdds,
                    AwayRank = groupMatchup.AwayRank,
                    HomeRank = groupMatchup.HomeRank,
                    StartDateUtc = groupMatchup.StartDateUtc
                });
            }

            groupWeek.AreMatchupsGenerated = true;

            await _dataContext.SaveChangesAsync();

        }
    }
}
