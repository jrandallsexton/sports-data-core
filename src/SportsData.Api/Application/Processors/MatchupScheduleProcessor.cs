using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
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
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IEventBus _eventBus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public MatchupScheduleProcessor(
            AppDataContext dataContext,
            ILogger<MatchupScheduleProcessor> logger,
            IContestClientFactory contestClientFactory,
            IEventBus eventBus,
            IDateTimeProvider dateTimeProvider)
        {
            _dataContext = dataContext;
            _logger = logger;
            _contestClientFactory = contestClientFactory;
            _eventBus = eventBus;
            _dateTimeProvider = dateTimeProvider;
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
                .Include(gw => gw.Matchups)
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
                // Refresh callers (e.g. SeasonPollWeekCreatedHandler — a new
                // AP poll just landed for this week) explicitly want to re-run
                // the filter to add newly-eligible matchups, so they bypass the
                // "already generated" short-circuit. The upsert-by-ContestId
                // loop below makes the second pass safe — existing matchups get
                // attribute updates, newly-eligible contests get inserted, and
                // contests that fell OUT of the filter are left in place because
                // user picks against them must be preserved.
                if (groupWeek.AreMatchupsGenerated && !command.IsRefresh)
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

            var matchupsResult = await _contestClientFactory
                .Resolve(group.Sport)
                .GetMatchupsForSeasonWeek(command.SeasonYear, command.SeasonWeek);
            if (!matchupsResult.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve matchups for season {Year} week {Week}. Skipping.", command.SeasonYear, command.SeasonWeek);
                return;
            }
            var allMatchups = matchupsResult.Value;

            // League window filter — excludes contests whose kickoff falls outside
            // [StartsOn, EndsOn]. Null bounds mean "no constraint" (full-season league),
            // so this is a no-op when neither is set.
            if (group.StartsOn.HasValue || group.EndsOn.HasValue)
            {
                var preCount = allMatchups.Count;
                allMatchups = allMatchups
                    .Where(m =>
                        (!group.StartsOn.HasValue || m.StartDateUtc >= group.StartsOn.Value) &&
                        (!group.EndsOn.HasValue || m.StartDateUtc <= group.EndsOn.Value))
                    .ToList();
                _logger.LogInformation(
                    "League window {StartsOn}..{EndsOn} filtered {Before} -> {After} matchups for group {GroupId} week {Week}",
                    group.StartsOn, group.EndsOn, preCount, allMatchups.Count, group.Id, command.SeasonWeek);
            }

            List<Matchup> groupMatchups;

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
                    )
                    .ToList();
            }
            else
            {
                groupMatchups = allMatchups
                    .Where(x =>
                        (x.AwayRank.HasValue && x.AwayRank <= topX) ||
                        (x.HomeRank.HasValue && x.HomeRank <= topX) ||
                        (x.AwayConferenceSlug != null && conferenceSlugs.Contains(x.AwayConferenceSlug)) ||
                        (x.HomeConferenceSlug != null && conferenceSlugs.Contains(x.HomeConferenceSlug))
                    )
                    .ToList();
            }

            // Upsert-by-ContestId. The shape supports both first-pass and
            // refresh calls:
            //   • Filter passes a contest already in groupWeek.Matchups →
            //     update mutable attributes (rank, spread, line, win/loss,
            //     headline, start time). Lets a post-poll refresh propagate
            //     newly-published ranks and odds without churning rows.
            //   • Filter passes a contest NOT yet in groupWeek.Matchups →
            //     insert. This is how a TOP25+SEC league's "ranked teams"
            //     get added the first time a poll lands after creation.
            //   • A contest already in groupWeek.Matchups that the filter
            //     REJECTS (e.g. Texas fell out of Top 25) is intentionally
            //     left in place — picks against it must be preserved.
            var existingByContestId = groupWeek.Matchups
                .ToDictionary(m => m.ContestId, m => m);
            var insertedCount = 0;

            foreach (var groupMatchup in groupMatchups)
            {
                if (existingByContestId.TryGetValue(groupMatchup.ContestId, out var existing))
                {
                    existing.AwayConferenceLosses = groupMatchup.AwayConferenceLosses;
                    existing.AwayConferenceWins = groupMatchup.AwayConferenceWins;
                    existing.AwayLosses = groupMatchup.AwayLosses;
                    existing.AwayRank = groupMatchup.AwayRank;
                    existing.AwaySpread = groupMatchup.AwaySpread;
                    existing.AwayWins = groupMatchup.AwayWins;
                    existing.Headline = groupMatchup.Headline;
                    existing.HomeConferenceLosses = groupMatchup.HomeConferenceLosses;
                    existing.HomeConferenceWins = groupMatchup.HomeConferenceWins;
                    existing.HomeLosses = groupMatchup.HomeLosses;
                    existing.HomeRank = groupMatchup.HomeRank;
                    existing.HomeSpread = groupMatchup.HomeSpread;
                    existing.HomeWins = groupMatchup.HomeWins;
                    existing.OverOdds = groupMatchup.OverOdds;
                    existing.OverUnder = groupMatchup.OverUnder;
                    existing.Spread = groupMatchup.Spread;
                    existing.StartDateUtc = groupMatchup.StartDateUtc;
                    existing.UnderOdds = groupMatchup.UnderOdds;
                    // Immutable on update: Id, ContestId, GroupId, SeasonWeekId,
                    // SeasonWeek, SeasonYear, CreatedBy, CreatedUtc.
                }
                else
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
                        CreatedUtc = _dateTimeProvider.UtcNow(),
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
                    insertedCount++;
                }
            }

            // Mark "generated" once a first-pass write succeeds. The flag is
            // a one-way switch that the daily scheduler reads to skip
            // already-populated weeks — refresh callers (IsRefresh=true)
            // explicitly bypass that gate at the top of this method.
            // Empty results leave the flag false so the daily scheduler
            // re-fires matchup generation on the next pass — the load-bearing
            // piece that makes the eager-bootstrap path work for
            // NCAAFB+RankingFilter shells created pre-poll. See
            // docs/league-creation-matrix.md "Processor change: AreMatchupsGenerated rule".
            if (groupMatchups.Count > 0)
            {
                groupWeek.AreMatchupsGenerated = true;
            }

            // Publish BEFORE SaveChanges so MassTransit's bus-outbox interceptor flushes
            // the captured message into the OutboxMessage table within the same transaction
            // as the entity write. If the save fails, the captured publish is rolled back
            // with it — same atomicity guarantee, correct outbox semantics.
            // Skip publish when nothing was inserted: the downstream consumer
            // (PickemGroupWeekMatchupsGeneratedHandler) enqueues preview jobs
            // for contests without existing previews. On a pure-update refresh
            // (filter returned matchups but they were all already present)
            // there are no new contests to preview, so re-publishing the event
            // would just trigger a query that finds nothing to do.
            var hasNewMatchups = insertedCount > 0;
            var isWeekCompleted = hasNewMatchups && groupMatchups.All(m => ContestStatusValues.IsCompleted(m.Status));
            if (hasNewMatchups && !isWeekCompleted)
            {
                await _eventBus.Publish(new PickemGroupWeekMatchupsGenerated(
                        group.Id,
                        command.SeasonWeek,
                        null,
                        group.Sport,
                        command.SeasonYear,
                        command.CorrelationId,
                        Guid.NewGuid()),
                    CancellationToken.None);
            }
            else if (!hasNewMatchups)
            {
                _logger.LogInformation(
                    "Skipping PickemGroupWeekMatchupsGenerated event — no new matchups inserted (filter returned {Count}, all already present). GroupId={GroupId}, SeasonYear={SeasonYear}, SeasonWeek={SeasonWeek}, IsRefresh={IsRefresh}",
                    groupMatchups.Count, group.Id, command.SeasonYear, command.SeasonWeek, command.IsRefresh);
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
