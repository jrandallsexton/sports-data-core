using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

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
        private readonly IEventBus _eventBus;

        public MatchupScheduleProcessor(
            AppDataContext dataContext,
            ILogger<MatchupScheduleProcessor> logger,
            IProvideCanonicalData canonicalDataProvider,
            IEventBus eventBus)
        {
            _dataContext = dataContext;
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _eventBus = eventBus;
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
                _logger.LogError("GroupWeek was null.{GroupId} {SeasonWeekId}", command.GroupId, command.SeasonWeekId);

                groupWeek = new PickemGroupWeek()
                {
                    Id = Guid.NewGuid(),
                    AreMatchupsGenerated = false,
                    SeasonWeek = command.SeasonWeek,
                    SeasonWeekId = command.SeasonWeekId,
                    SeasonYear = command.SeasonYear,
                    GroupId = command.GroupId,
                    IsNonStandardWeek = command.IsNonStandardWeek
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

            var allMatchups = await _canonicalDataProvider.GetMatchupsForSeasonWeek(command.SeasonYear, command.SeasonWeek);

            IEnumerable<Matchup>? groupMatchups;

            if (groupWeek.IsNonStandardWeek && !string.IsNullOrEmpty(group.NonStandardWeekGroupSeasonMapFilter))
            {
                var groupFilters = group.NonStandardWeekGroupSeasonMapFilter
                    .Split('|', StringSplitOptions.RemoveEmptyEntries)
                    .Select(f => f.Trim())
                    .ToArray();
                
                // this could be ["fbs"] or ["fbs", "foo", "bar"], etc.
                // AwayGroupSeasonMap and HomeGroupSeasonMap look like this: "NCAAF|yy|d3" or "NCAAF|NCAA|fbs|American" (not exclusive examples)
                groupMatchups = allMatchups
                    .Where(x =>
                        (x.AwayRank.HasValue && x.AwayRank <= topX) ||
                        (x.HomeRank.HasValue && x.HomeRank <= topX) ||
                        (x.AwayConferenceSlug != null && conferenceSlugs.Contains(x.AwayConferenceSlug)) ||
                        (x.HomeConferenceSlug != null && conferenceSlugs.Contains(x.HomeConferenceSlug)) ||
                        (x.AwayGroupSeasonMap != null && groupFilters.Any(filter => x.AwayGroupSeasonMap.Contains(filter, StringComparison.OrdinalIgnoreCase))) ||
                        (x.HomeGroupSeasonMap != null && groupFilters.Any(filter => x.HomeGroupSeasonMap.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    );
            }
            else
            {
                groupMatchups = allMatchups
                    .Where(x =>
                        (x.AwayRank.HasValue && x.AwayRank <= topX) ||
                        (x.HomeRank.HasValue && x.HomeRank <= topX) ||
                        (x.AwayConferenceSlug != null && conferenceSlugs.Contains(x.AwayConferenceSlug)) ||
                        (x.HomeConferenceSlug != null && conferenceSlugs.Contains(x.HomeConferenceSlug))
                    );
            }

            foreach (var groupMatchup in groupMatchups)
            {
                groupWeek.Matchups.Add(new PickemGroupMatchup()
                {
                    Id = Guid.NewGuid(),
                    AwayConferenceLosses = groupMatchup.AwayConferenceLosses,
                    AwayConferenceWins = groupMatchup.AwayConferenceWins,
                    AwayLosses = groupMatchup.AwayLosses,
                    AwayRank = groupMatchup.AwayRank,
                    AwaySpread = groupMatchup.AwaySpread,
                    AwayWins = groupMatchup.AwayWins,
                    ContestId = groupMatchup.ContestId,
                    CreatedBy = Guid.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    GroupId = group.Id,
                    GroupWeek = groupWeek,
                    Headline = groupMatchup.Headline,
                    HomeConferenceLosses = groupMatchup.HomeConferenceLosses,
                    HomeConferenceWins = groupMatchup.HomeConferenceWins,
                    HomeLosses = groupMatchup.HomeLosses,
                    HomeRank = groupMatchup.HomeRank,
                    HomeSpread = groupMatchup.HomeSpread,
                    HomeWins = groupMatchup.HomeWins,
                    OverOdds = groupMatchup.OverOdds,
                    OverUnder = groupMatchup.OverUnder,
                    SeasonWeek = groupWeek.SeasonWeek,
                    SeasonWeekId = groupMatchup.SeasonWeekId,
                    SeasonYear = command.SeasonYear,
                    Spread = groupMatchup.Spread,
                    StartDateUtc = groupMatchup.StartDateUtc,
                    UnderOdds = groupMatchup.UnderOdds
                });
            }

            groupWeek.AreMatchupsGenerated = true;

            // Only publish event if the week is not completed
            var isWeekCompleted = allMatchups.All(m => m.Status == "Final" || m.Status == "Completed");
            if (!isWeekCompleted)
            {
                await _eventBus.Publish(new PickemGroupWeekMatchupsGenerated(
                        group.Id,
                        command.SeasonYear,
                        command.SeasonWeek,
                        command.CorrelationId,
                        Guid.NewGuid()),
                    CancellationToken.None);
            }
            else
            {
                _logger.LogInformation("Skipping PickemGroupWeekMatchupsGenerated event for completed week. GroupId={GroupId}, SeasonYear={SeasonYear}, SeasonWeek={SeasonWeek}",
                    group.Id, command.SeasonYear, command.SeasonWeek);
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
