using MassTransit;

using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;

namespace SportsData.Producer.Application.Events
{
    /// <summary>
    /// Producer-side consumer of <see cref="ContestFinalized"/>. Enqueues
    /// record enrichment (<see cref="IEnrichFranchiseSeasons"/>: W/L and
    /// conference W/L re-derived from finalized contests) for BOTH
    /// participants, so a team's record is current minutes after its game
    /// finalizes instead of waiting for the weekly
    /// <c>FranchiseSeasonEnrichmentJob</c>, which stays as the backstop.
    ///
    /// Record leg only, deliberately. The weekly job's other two legs do not
    /// belong on this trigger: franchise season metrics average per-game
    /// CompetitionMetric rows that do not exist yet for this game (the
    /// metrics audit computes them hours later, after play-by-play lands),
    /// and the ESPN season-statistics refresh would be a live fetch per team
    /// per game against stats that likely lag the final whistle.
    ///
    /// Ordering is safe: the enrichment processors publish ContestFinalized
    /// through the outbox in the same SaveChangesAsync that sets
    /// FinalizedUtc, and the record handler counts only finalized contests,
    /// so this game is always included.
    ///
    /// Enqueued BY INTERFACE so Hangfire resolves the sport's registered
    /// closed type; enqueuing a concrete EnrichFranchiseSeasonHandler type
    /// constructed it over the abstract context and lost its outbox publish
    /// of FranchiseSeasonEnrichmentCompleted (see FranchiseSeasonEnrichmentJob).
    ///
    /// Named for the event it handles, by decision (2026-09-27, PR #796).
    /// Known trade-off: the kebab-case endpoint formatter derives the queue
    /// name from the class name, and the API also consumes this event as
    /// ContestFinalizedHandler, so both map to "contest-finalized-handler".
    /// In production that is harmless: the API's queue is on the API broker
    /// and this one is on the sport's Producer broker, bridged by
    /// exchange-level shovels. Where they share ONE broker (the local docker
    /// stack), the two become competing consumers on a single queue and each
    /// message reaches only one of them, so local pick scoring and local
    /// record enrichment each see only some finalizations. Accepted; revisit
    /// (e.g. a ConsumerDefinition endpoint name) if the broker layout changes.
    ///
    /// Per the "ingest consumers must be thin Hangfire-spawn shims"
    /// convention, this consumer does no inline DB work. Re-deliveries and
    /// re-enrichments of the same contest re-run an idempotent recompute.
    /// </summary>
    public class ContestFinalizedHandler : IConsumer<ContestFinalized>
    {
        private readonly ILogger<ContestFinalizedHandler> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestFinalizedHandler(
            ILogger<ContestFinalizedHandler> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public Task Consume(ConsumeContext<ContestFinalized> context)
        {
            var msg = context.Message;

            _logger.LogInformation(
                "ContestFinalized consume: received. ContestId={ContestId}, Sport={Sport}, SeasonYear={SeasonYear}, AwayFranchiseSeasonId={AwayFranchiseSeasonId}, HomeFranchiseSeasonId={HomeFranchiseSeasonId}, CorrelationId={CorrelationId}, MessageId={MessageId}",
                msg.ContestId, msg.Sport, msg.SeasonYear, msg.AwayFranchiseSeasonId, msg.HomeFranchiseSeasonId, msg.CorrelationId, context.MessageId);

            if (msg.SeasonYear is not { } seasonYear)
            {
                _logger.LogWarning(
                    "ContestFinalized consume: no SeasonYear; franchise season record enrichment skipped. ContestId={ContestId}",
                    msg.ContestId);
                return Task.CompletedTask;
            }

            foreach (var franchiseSeasonId in new[] { msg.AwayFranchiseSeasonId, msg.HomeFranchiseSeasonId })
            {
                // Null from a Producer pod publishing the pre-2026-09 shape
                // during a rolling deploy; the weekly job covers that team.
                if (franchiseSeasonId is not { } id || id == Guid.Empty)
                {
                    _logger.LogWarning(
                        "ContestFinalized consume: participant FranchiseSeasonId missing; record enrichment skipped for that side. ContestId={ContestId}",
                        msg.ContestId);
                    continue;
                }

                var cmd = new EnrichFranchiseSeasonCommand(id, seasonYear, msg.CorrelationId);
                _backgroundJobProvider.Enqueue<IEnrichFranchiseSeasons>(p => p.Process(cmd));
            }

            _logger.LogInformation(
                "ContestFinalized consume: franchise season record enrichment enqueued. ContestId={ContestId}, CorrelationId={CorrelationId}",
                msg.ContestId, msg.CorrelationId);

            return Task.CompletedTask;
        }
    }
}
