using System.Diagnostics.Metrics;
using MassTransit;
using Microsoft.Extensions.Options;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Orchestrates historical season sourcing across three tiers:
/// Season → Venue → TeamSeason.
/// Uses completion events from Producer to trigger each subsequent tier progressively.
///
/// There is deliberately NO AthleteSeason tier. ESPN's league-level
/// /seasons/{year}/athletes index is NOT season-scoped — it returns the
/// entire athlete database under every season path, with each athlete's
/// CURRENT team rendered as Team.$ref. Sourcing it fabricated millions of
/// AthleteSeason rows binding modern athletes to historical seasons (see
/// docs/audit/athlete-season-fabrication-remediation.md). Athlete-seasons
/// are born exclusively from the TeamSeason cascade's per-team roster ref,
/// which IS season-scoped (and empty for pre-roster-era seasons).
/// </summary>
public class HistoricalSeasonSourcingSaga : MassTransitStateMachine<HistoricalSeasonSourcingState>
{
    private readonly ILogger<HistoricalSeasonSourcingSaga> _logger;
    private readonly HistoricalSourcingConfig _config;
    private static readonly Meter _meter = new("SportsData.Provider.Sagas");
    private static readonly Counter<int> _sagaStartedCounter = _meter.CreateCounter<int>("saga.historical_sourcing.started", description: "Number of historical sourcing sagas started");
    private static readonly Counter<int> _sagaCompletedCounter = _meter.CreateCounter<int>("saga.historical_sourcing.completed", description: "Number of historical sourcing sagas completed");
    private static readonly Histogram<double> _sagaDurationHistogram = _meter.CreateHistogram<double>("saga.historical_sourcing.duration_seconds", unit: "s", description: "Duration of historical sourcing sagas");
    private static readonly Counter<int> _tierCompletedCounter = _meter.CreateCounter<int>("saga.historical_sourcing.tier_completed", description: "Number of historical sourcing saga tiers completed");

    public State WaitingForSeasonCompletion { get; private set; } = null!;
    public State WaitingForVenueCompletion { get; private set; } = null!;
    public State WaitingForTeamSeasonCompletion { get; private set; } = null!;
    // Legacy state: the AthleteSeason tier was removed (see class doc). The
    // state stays DECLARED so persisted saga instances parked in it still
    // resolve by name; nothing transitions into it anymore.
    public State WaitingForAthleteSeasonCompletion { get; private set; } = null!;

    public Event<SeasonSourcingStarted> SourcingStarted { get; private set; } = null!;
    public Event<DocumentProcessingCompleted> DocumentCompleted { get; private set; } = null!;

    public HistoricalSeasonSourcingSaga(
        ILogger<HistoricalSeasonSourcingSaga> logger,
        IOptions<HistoricalSourcingConfig> config)
    {
        _logger = logger;
        _config = config.Value;

        InstanceState(x => x.CurrentState);

        Event(() => SourcingStarted, x => x.CorrelateById(context => context.Message.CorrelationId));
        
        Event(() => DocumentCompleted, x =>
        {
            x.CorrelateBy((state, context) =>
                state.Sport == context.Message.Sport &&
                state.SeasonYear == context.Message.SeasonYear &&
                state.Provider == context.Message.Provider);
            x.OnMissingInstance(m => m.Discard());
        });

        Initially(
            When(SourcingStarted)
                .Then(context =>
                {
                    context.Saga.Sport = context.Message.Sport;
                    context.Saga.SeasonYear = context.Message.SeasonYear;
                    context.Saga.Provider = context.Message.Provider;
                    context.Saga.StartedUtc = DateTime.UtcNow;
                    
                    _sagaStartedCounter.Add(1, 
                        new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                        new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                        new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()));

                    _logger.LogInformation(
                        "🚀 SAGA_STARTED: Historical sourcing saga initiated. " +
                        "CorrelationId={CorrelationId}, Sport={Sport}, Season={Season}, Provider={Provider}",
                        context.Saga.CorrelationId,
                        context.Saga.Sport,
                        context.Saga.SeasonYear,
                        context.Saga.Provider);
                })
                .PublishAsync(context => context.Init<TriggerTierSourcing>(new
                {
                    context.Saga.CorrelationId,
                    context.Saga.Sport,
                    context.Saga.SeasonYear,
                    context.Saga.Provider,
                    Tier = 1,
                    TierName = "Season"
                }))
                .TransitionTo(WaitingForSeasonCompletion)
        );

        // NOTE: The four During() blocks below follow an identical pattern but cannot be easily extracted
        // due to MassTransit's strongly-typed lambda requirements. Each tier accesses different saga properties
        // (e.g., SeasonCompletionEventsReceived vs VenueCompletionEventsReceived) and different DocumentType filters.
        // Extracting this would require complex Action<>/Func<> delegates or expression trees that would be
        // harder to read/maintain than the current explicit duplication. This is a case where duplication
        // is preferable to the wrong abstraction.

        // Tier 1: Season → Venue
        During(WaitingForSeasonCompletion,
            When(DocumentCompleted, context => context.Message.DocumentType == DocumentType.Season)
                .Then(context =>
                {
                    context.Saga.SeasonCompletionEventsReceived++;
                    
                    _logger.LogInformation(
                        "✅ TIER1_PROGRESS: Season completion event received ({Count}/{Threshold}). " +
                        "CorrelationId={CorrelationId}, DocumentType={DocumentType}",
                        context.Saga.SeasonCompletionEventsReceived,
                        _config.SagaConfig.CompletionThreshold,
                        context.Saga.CorrelationId,
                        context.Message.DocumentType);
                })
                .If(context => context.Saga.SeasonCompletionEventsReceived >= _config.SagaConfig.CompletionThreshold,
                    binder => binder
                        .Then(context =>
                        {
                            context.Saga.SeasonCompletedUtc = DateTime.UtcNow;
                            
                            _tierCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()),
                                new KeyValuePair<string, object?>("tier", "Season"));

                            _logger.LogInformation(
                                "🎯 TIER1_COMPLETE: Season tier completed, triggering Venue tier. " +
                                "CorrelationId={CorrelationId}, EventsReceived={EventsReceived}, Duration={Duration}s",
                                context.Saga.CorrelationId,
                                context.Saga.SeasonCompletionEventsReceived,
                                (context.Saga.SeasonCompletedUtc.Value - context.Saga.StartedUtc).TotalSeconds);
                        })
                        .PublishAsync(context => context.Init<TriggerTierSourcing>(new
                        {
                            context.Saga.CorrelationId,
                            context.Saga.Sport,
                            context.Saga.SeasonYear,
                            context.Saga.Provider,
                            Tier = 2,
                            TierName = "Venue"
                        }))
                        .TransitionTo(WaitingForVenueCompletion))
        );

        // Tier 2: Venue → TeamSeason
        During(WaitingForVenueCompletion,
            When(DocumentCompleted, context => context.Message.DocumentType == DocumentType.Venue)
                .Then(context =>
                {
                    context.Saga.VenueCompletionEventsReceived++;
                    
                    _logger.LogInformation(
                        "✅ TIER2_PROGRESS: Venue completion event received ({Count}/{Threshold}). " +
                        "CorrelationId={CorrelationId}, DocumentType={DocumentType}",
                        context.Saga.VenueCompletionEventsReceived,
                        _config.SagaConfig.CompletionThreshold,
                        context.Saga.CorrelationId,
                        context.Message.DocumentType);
                })
                .If(context => context.Saga.VenueCompletionEventsReceived >= _config.SagaConfig.CompletionThreshold,
                    binder => binder
                        .Then(context =>
                        {
                            context.Saga.VenueCompletedUtc = DateTime.UtcNow;
                            
                            _tierCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()),
                                new KeyValuePair<string, object?>("tier", "Venue"));

                            _logger.LogInformation(
                                "🎯 TIER2_COMPLETE: Venue tier completed, triggering TeamSeason tier. " +
                                "CorrelationId={CorrelationId}, EventsReceived={EventsReceived}, Duration={Duration}s",
                                context.Saga.CorrelationId,
                                context.Saga.VenueCompletionEventsReceived,
                                (context.Saga.VenueCompletedUtc.Value - context.Saga.SeasonCompletedUtc!.Value).TotalSeconds);
                        })
                        .PublishAsync(context => context.Init<TriggerTierSourcing>(new
                        {
                            context.Saga.CorrelationId,
                            context.Saga.Sport,
                            context.Saga.SeasonYear,
                            context.Saga.Provider,
                            Tier = 3,
                            TierName = "TeamSeason"
                        }))
                        .TransitionTo(WaitingForTeamSeasonCompletion))
        );

        // Tier 3: TeamSeason → AthleteSeason
        During(WaitingForTeamSeasonCompletion,
            When(DocumentCompleted, context => context.Message.DocumentType == DocumentType.TeamSeason)
                .Then(context =>
                {
                    context.Saga.TeamSeasonCompletionEventsReceived++;
                    
                    _logger.LogInformation(
                        "✅ TIER3_PROGRESS: TeamSeason completion event received ({Count}/{Threshold}). " +
                        "CorrelationId={CorrelationId}, DocumentType={DocumentType}",
                        context.Saga.TeamSeasonCompletionEventsReceived,
                        _config.SagaConfig.CompletionThreshold,
                        context.Saga.CorrelationId,
                        context.Message.DocumentType);
                })
                .If(context => context.Saga.TeamSeasonCompletionEventsReceived >= _config.SagaConfig.CompletionThreshold,
                    binder => binder
                        .Then(context =>
                        {
                            context.Saga.TeamSeasonCompletedUtc = DateTime.UtcNow;
                            
                            _tierCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()),
                                new KeyValuePair<string, object?>("tier", "TeamSeason"));

                            context.Saga.CompletedUtc = DateTime.UtcNow;
                            var totalDuration = (context.Saga.CompletedUtc.Value - context.Saga.StartedUtc).TotalSeconds;

                            _sagaCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()));

                            _sagaDurationHistogram.Record(totalDuration,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()));

                            _logger.LogInformation(
                                "🏁 SAGA_COMPLETE: TeamSeason tier completed — final tier (AthleteSeason " +
                                "tier removed; athlete-seasons flow from the TeamSeason roster cascade). " +
                                "CorrelationId={CorrelationId}, Sport={Sport}, Season={Season}, " +
                                "TotalDuration={TotalDuration}s, " +
                                "Tier1Events={Tier1Events}, Tier2Events={Tier2Events}, Tier3Events={Tier3Events}",
                                context.Saga.CorrelationId,
                                context.Saga.Sport,
                                context.Saga.SeasonYear,
                                totalDuration,
                                context.Saga.SeasonCompletionEventsReceived,
                                context.Saga.VenueCompletionEventsReceived,
                                context.Saga.TeamSeasonCompletionEventsReceived);
                        })
                        .Finalize())
        );

        // LEGACY DRAIN: instances persisted in the removed Tier 4 state.
        // Nothing schedules AthleteSeason work anymore, but a saga that had
        // already triggered Tier 4 before this deploy still receives
        // completion events for the in-flight documents — count them and
        // finalize at threshold exactly as the old tier did, so those
        // instances terminate instead of parking forever.
        During(WaitingForAthleteSeasonCompletion,
            When(DocumentCompleted, context => context.Message.DocumentType == DocumentType.AthleteSeason)
                .Then(context =>
                {
                    context.Saga.AthleteSeasonCompletionEventsReceived++;

                    _logger.LogInformation(
                        "LEGACY_TIER4_DRAIN: AthleteSeason completion received ({Count}/{Threshold}) for a pre-remediation saga instance. " +
                        "CorrelationId={CorrelationId}",
                        context.Saga.AthleteSeasonCompletionEventsReceived,
                        _config.SagaConfig.CompletionThreshold,
                        context.Saga.CorrelationId);
                })
                .If(context => context.Saga.AthleteSeasonCompletionEventsReceived >= _config.SagaConfig.CompletionThreshold,
                    binder => binder
                        .Then(context =>
                        {
                            context.Saga.CompletedUtc = DateTime.UtcNow;
                            var totalDuration = (context.Saga.CompletedUtc.Value - context.Saga.StartedUtc).TotalSeconds;

                            // Same terminal telemetry as the active completion
                            // path — a drained legacy instance is still a
                            // completed saga on the dashboards.
                            _tierCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()),
                                new KeyValuePair<string, object?>("tier", "AthleteSeason"));

                            _sagaCompletedCounter.Add(1,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()));

                            _sagaDurationHistogram.Record(totalDuration,
                                new KeyValuePair<string, object?>("sport", context.Saga.Sport.ToString()),
                                new KeyValuePair<string, object?>("season", context.Saga.SeasonYear),
                                new KeyValuePair<string, object?>("provider", context.Saga.Provider.ToString()));

                            _logger.LogInformation(
                                "LEGACY_TIER4_DRAIN: pre-remediation saga instance finalized. CorrelationId={CorrelationId}, TotalDuration={TotalDuration}s",
                                context.Saga.CorrelationId,
                                totalDuration);
                        })
                        .Finalize())
        );
    }
}

/// <summary>
/// Internal event to trigger sourcing of the next tier.
/// Consumed by ResourceIndexJob to start processing the specified tier.
/// </summary>
public record TriggerTierSourcing
{
    public Guid CorrelationId { get; init; }
    public Sport Sport { get; init; }
    public int SeasonYear { get; init; }
    public SourceDataProvider Provider { get; init; }
    public int Tier { get; init; }
    public string TierName { get; init; } = string.Empty;
}
