using MassTransit;

using SportsData.Api.Application.Processors;
using SportsData.Core.Eventing.Events.Franchise;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// A franchise season was re-enriched on the Producer (the weekly job, or
    /// the admin action on the team page), so the record every league card
    /// shows for that team may have changed. The card reads the
    /// PickemGroupMatchup snapshot and never derives (#769), so evicting the
    /// cache alone would re-serve the same stale snapshot: the snapshot has to
    /// be corrected first. Both are the record audit's job; this consumer
    /// only decides the scope.
    /// </summary>
    /// <remarks>
    /// The event carries every contest the team plays this season (any
    /// status), so no Producer round trip is needed to find the affected
    /// matchups: the audit intersects those ids with the league matchup rows
    /// and calls the Producer for entering records only when something
    /// matched. The weekly job fires this once per team (~800 for NCAAFB);
    /// for the vast majority nothing is in any league and the consume is one
    /// indexed query. Idempotent by construction (the audit corrects only
    /// rows that differ and re-evicts what it examined), so at-least-once
    /// delivery is safe.
    /// </remarks>
    public class FranchiseSeasonEnrichmentCompletedHandler : IConsumer<FranchiseSeasonEnrichmentCompleted>
    {
        private readonly ILogger<FranchiseSeasonEnrichmentCompletedHandler> _logger;
        private readonly IAuditMatchupRecords _auditor;

        public FranchiseSeasonEnrichmentCompletedHandler(
            ILogger<FranchiseSeasonEnrichmentCompletedHandler> logger,
            IAuditMatchupRecords auditor)
        {
            _logger = logger;
            _auditor = auditor;
        }

        public async Task Consume(ConsumeContext<FranchiseSeasonEnrichmentCompleted> context)
        {
            var msg = context.Message;

            if (msg.ContestIds is null || msg.ContestIds.Count == 0)
            {
                // A pre-#786 Producer, or a team with no contests yet. Nothing
                // on a league card can reference this team, so nothing to do.
                _logger.LogDebug(
                    "FranchiseSeasonEnrichmentCompleted carried no contest ids; nothing to audit. FranchiseSeasonId={FranchiseSeasonId}, CorrelationId={CorrelationId}",
                    msg.FranchiseSeasonId, msg.CorrelationId);
                return;
            }

            _logger.LogInformation(
                "FranchiseSeasonEnrichmentCompleted consume: auditing league matchup records for {Count} contest(s). FranchiseSeasonId={FranchiseSeasonId}, Sport={Sport}, SeasonYear={SeasonYear}, CorrelationId={CorrelationId}",
                msg.ContestIds.Count, msg.FranchiseSeasonId, msg.Sport, msg.SeasonYear, msg.CorrelationId);

            // Throws on a Producer failure (the audit refuses to report work
            // it did not persist); MassTransit retries, and the audit is
            // idempotent, so that is the right outcome.
            var result = await _auditor.Process(new MatchupRecordAuditByContestsCommand(msg.Sport, msg.ContestIds));

            _logger.LogInformation(
                "FranchiseSeasonEnrichmentCompleted consume: audit done. FranchiseSeasonId={FranchiseSeasonId}, Examined={Examined}, Corrected={Corrected}, Unresolved={Unresolved}, CorrelationId={CorrelationId}",
                msg.FranchiseSeasonId, result.Examined, result.Corrected, result.Unresolved, msg.CorrelationId);
        }
    }
}
