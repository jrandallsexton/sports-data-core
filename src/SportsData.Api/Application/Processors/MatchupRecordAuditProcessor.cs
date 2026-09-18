using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Processors
{
    public interface IAuditMatchupRecords
    {
        Task<MatchupRecordAuditResult> Process(MatchupRecordAuditCommand command);
    }

    /// <summary>
    /// Brings the record snapshots on PickemGroupMatchup back in line with the
    /// record each team actually carried into the game.
    /// </summary>
    /// <remarks>
    /// The snapshot is what league cards display — the read path deliberately
    /// projects the stored column and never derives (#769). That makes a wrong
    /// snapshot permanently wrong, and two write-side defects produced a lot of
    /// them:
    /// <list type="bullet">
    /// <item>the upsert refreshed records on every pass, so an already-played
    /// week kept absorbing later values (NCAAFB group b3d8288d week 1 held 3-0
    /// and 0-3 for games played weeks earlier) — fixed in #771, which stops new
    /// damage but repairs nothing;</item>
    /// <item>rows predating the MatchupRecordSnapshots migration were added with
    /// defaultValue 0 and never backfilled (111 in prod, all 2025).</item>
    /// </list>
    /// <para>
    /// Producer owns the derivation because the API cannot read its database.
    /// It counts prior finalized, non-preseason contests, so this is safe to
    /// re-run and worth re-running: a contest still cycling through the
    /// enrichment audit is not finalized at that moment and leaves its teams a
    /// game light until it settles.
    /// </para>
    /// <para>
    /// Corrects only rows that actually differ, so a clean week writes nothing
    /// and ModifiedUtc stays meaningful.
    /// </para>
    /// </remarks>
    public class MatchupRecordAuditProcessor : IAuditMatchupRecords
    {
        /// <summary>Matches the Producer handler's per-request cap.</summary>
        private const int BatchSize = 500;

        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupRecordAuditProcessor> _logger;
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILeagueWeekMatchupsCache _matchupsCache;

        public MatchupRecordAuditProcessor(
            AppDataContext dataContext,
            ILogger<MatchupRecordAuditProcessor> logger,
            IContestClientFactory contestClientFactory,
            IDateTimeProvider dateTimeProvider,
            ILeagueWeekMatchupsCache matchupsCache)
        {
            _dataContext = dataContext;
            _logger = logger;
            _contestClientFactory = contestClientFactory;
            _dateTimeProvider = dateTimeProvider;
            _matchupsCache = matchupsCache;
        }

        public async Task<MatchupRecordAuditResult> Process(MatchupRecordAuditCommand command)
        {
            var scope = _dataContext.PickemGroupMatchups
                .Where(m => m.SeasonYear == command.SeasonYear);

            if (command.SeasonWeek.HasValue)
                scope = scope.Where(m => m.SeasonWeek == command.SeasonWeek.Value);

            // Sport lives on the group, not the matchup, and each sport is a
            // separate Producer instance — so a batch must not mix them.
            var rows = await scope
                .Join(_dataContext.PickemGroups.Where(g => g.Sport == command.Sport),
                    m => m.GroupId,
                    g => g.Id,
                    (m, g) => m)
                .OrderBy(m => m.SeasonWeek)
                .ToListAsync();

            if (rows.Count == 0)
            {
                _logger.LogInformation(
                    "Matchup record audit: nothing in scope. Sport={Sport}, SeasonYear={SeasonYear}, SeasonWeek={SeasonWeek}",
                    command.Sport, command.SeasonYear, command.SeasonWeek);
                return new MatchupRecordAuditResult(0, 0, 0);
            }

            var client = _contestClientFactory.Resolve(command.Sport);
            var contestIds = rows.Select(m => m.ContestId).Distinct().ToList();

            var corrected = 0;
            var unresolved = 0;

            // Every league-week in scope, not just the ones corrected. The cache
            // swallows eviction failures by design (it must never fault the
            // write that triggered it), so evicting only what changed made a
            // dropped eviction unrecoverable: the rerun finds the rows already
            // correct, evicts nothing, and the stale payload serves out its TTL.
            // This audit is manual and narrow, so re-evicting a clean week costs
            // one rebuild and removes that failure mode entirely.
            var leagueWeeksInScope = rows
                .Select(m => (m.GroupId, m.SeasonWeek))
                .Distinct()
                .ToList();

            foreach (var page in contestIds.Chunk(BatchSize))
            {
                var result = await client.GetEnteringRecordsByContestIds(page.ToList());

                if (!result.IsSuccess)
                {
                    // Throw rather than return. Continuing would let a failed
                    // page read as "these contests have no corrections", and
                    // returning a normal result would report the corrections
                    // applied from EARLIER pages — which are still only in the
                    // change tracker, since SaveChangesAsync has not run. The
                    // throw discards them with the scope and surfaces as a
                    // non-success response instead of a 200 claiming work that
                    // was never persisted.
                    _logger.LogError(
                        "Matchup record audit aborted — Producer call failed for a page of {Count} contest(s). " +
                        "Sport={Sport}, SeasonYear={SeasonYear}, SeasonWeek={SeasonWeek}, PendingUnsaved={Pending}",
                        page.Length, command.Sport, command.SeasonYear, command.SeasonWeek, corrected);

                    throw new InvalidOperationException(
                        $"Matchup record audit aborted: Producer failed for a page of {page.Length} contest(s). " +
                        $"No corrections were saved.");
                }

                var byContestId = result.Value.ToDictionary(r => r.ContestId);

                foreach (var matchup in rows.Where(m => byContestId.ContainsKey(m.ContestId)))
                {
                    var derived = byContestId[matchup.ContestId];

                    var differs =
                        matchup.AwayWins != derived.AwayWins ||
                        matchup.AwayLosses != derived.AwayLosses ||
                        matchup.AwayConferenceWins != derived.AwayConferenceWins ||
                        matchup.AwayConferenceLosses != derived.AwayConferenceLosses ||
                        matchup.HomeWins != derived.HomeWins ||
                        matchup.HomeLosses != derived.HomeLosses ||
                        matchup.HomeConferenceWins != derived.HomeConferenceWins ||
                        matchup.HomeConferenceLosses != derived.HomeConferenceLosses;

                    if (!differs)
                        continue;

                    _logger.LogInformation(
                        "Matchup record corrected. GroupId={GroupId}, ContestId={ContestId}, Week={Week}, " +
                        "Was={WasAwayWins}-{WasAwayLosses} / {WasHomeWins}-{WasHomeLosses}, " +
                        "Now={NowAwayWins}-{NowAwayLosses} / {NowHomeWins}-{NowHomeLosses}",
                        matchup.GroupId, matchup.ContestId, matchup.SeasonWeek,
                        matchup.AwayWins, matchup.AwayLosses, matchup.HomeWins, matchup.HomeLosses,
                        derived.AwayWins, derived.AwayLosses, derived.HomeWins, derived.HomeLosses);

                    matchup.AwayWins = derived.AwayWins;
                    matchup.AwayLosses = derived.AwayLosses;
                    matchup.AwayConferenceWins = derived.AwayConferenceWins;
                    matchup.AwayConferenceLosses = derived.AwayConferenceLosses;
                    matchup.HomeWins = derived.HomeWins;
                    matchup.HomeLosses = derived.HomeLosses;
                    matchup.HomeConferenceWins = derived.HomeConferenceWins;
                    matchup.HomeConferenceLosses = derived.HomeConferenceLosses;
                    matchup.ModifiedUtc = _dateTimeProvider.UtcNow();
                    matchup.ModifiedBy = Guid.Empty;

                    corrected++;
                }

                unresolved += page.Length - byContestId.Count;
            }

            await _dataContext.SaveChangesAsync();

            foreach (var (groupId, seasonWeek) in leagueWeeksInScope)
                await _matchupsCache.RemoveAsync(groupId, seasonWeek);

            _logger.LogInformation(
                "Matchup record audit complete. Sport={Sport}, SeasonYear={SeasonYear}, SeasonWeek={SeasonWeek}, " +
                "Examined={Examined}, Corrected={Corrected}, Unresolved={Unresolved}, LeagueWeeksEvicted={Evicted}",
                command.Sport, command.SeasonYear, command.SeasonWeek,
                rows.Count, corrected, unresolved, leagueWeeksInScope.Count);

            return new MatchupRecordAuditResult(rows.Count, corrected, unresolved);
        }
    }

    /// <param name="SeasonWeek">Null audits every week of the season year.</param>
    public record MatchupRecordAuditCommand(
        Sport Sport,
        int SeasonYear,
        int? SeasonWeek = null);

    /// <param name="Unresolved">
    /// Contests Producer could not derive a record for. Non-zero means the two
    /// services disagree about what exists, which is worth looking at.
    /// </param>
    public record MatchupRecordAuditResult(
        int Examined,
        int Corrected,
        int Unresolved);
}
