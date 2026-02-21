using MassTransit;
using Microsoft.Extensions.Options;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Orchestrates historical season sourcing across four tiers: Season → Venue → TeamSeason → AthleteSeason.
/// Uses completion events from Producer to trigger each subsequent tier progressively.
/// </summary>
public class HistoricalSeasonSourcingSaga : MassTransitStateMachine<HistoricalSeasonSourcingState>
{
    private readonly ILogger<HistoricalSeasonSourcingSaga> _logger;
    private readonly HistoricalSourcingConfig _config;

    public State WaitingForSeasonCompletion { get; private set; } = null!;
    public State WaitingForVenueCompletion { get; private set; } = null!;
    public State WaitingForTeamSeasonCompletion { get; private set; } = null!;
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
                state.SeasonYear == context.Message.SeasonYear);
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
                    
                    _logger.LogInformation(
                        "🚀 SAGA_STARTED: Historical sourcing saga initiated. " +
                        "CorrelationId={CorrelationId}, Sport={Sport}, Season={Season}, Provider={Provider}",
                        context.Saga.CorrelationId,
                        context.Saga.Sport,
                        context.Saga.SeasonYear,
                        context.Saga.Provider);
                })
                .TransitionTo(WaitingForSeasonCompletion)
        );

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
                            
                            _logger.LogInformation(
                                "🎯 TIER3_COMPLETE: TeamSeason tier completed, triggering AthleteSeason tier. " +
                                "CorrelationId={CorrelationId}, EventsReceived={EventsReceived}, Duration={Duration}s",
                                context.Saga.CorrelationId,
                                context.Saga.TeamSeasonCompletionEventsReceived,
                                (context.Saga.TeamSeasonCompletedUtc.Value - context.Saga.VenueCompletedUtc!.Value).TotalSeconds);
                        })
                        .PublishAsync(context => context.Init<TriggerTierSourcing>(new
                        {
                            context.Saga.CorrelationId,
                            context.Saga.Sport,
                            context.Saga.SeasonYear,
                            context.Saga.Provider,
                            Tier = 4,
                            TierName = "AthleteSeason"
                        }))
                        .TransitionTo(WaitingForAthleteSeasonCompletion))
        );

        // Tier 4: AthleteSeason → Completed
        During(WaitingForAthleteSeasonCompletion,
            When(DocumentCompleted, context => context.Message.DocumentType == DocumentType.AthleteSeason)
                .Then(context =>
                {
                    context.Saga.AthleteSeasonCompletionEventsReceived++;
                    
                    _logger.LogInformation(
                        "✅ TIER4_PROGRESS: AthleteSeason completion event received ({Count}/{Threshold}). " +
                        "CorrelationId={CorrelationId}, DocumentType={DocumentType}",
                        context.Saga.AthleteSeasonCompletionEventsReceived,
                        _config.SagaConfig.CompletionThreshold,
                        context.Saga.CorrelationId,
                        context.Message.DocumentType);
                })
                .If(context => context.Saga.AthleteSeasonCompletionEventsReceived >= _config.SagaConfig.CompletionThreshold,
                    binder => binder
                        .Then(context =>
                        {
                            context.Saga.CompletedUtc = DateTime.UtcNow;
                            
                            var totalDuration = (context.Saga.CompletedUtc.Value - context.Saga.StartedUtc).TotalSeconds;
                            
                            _logger.LogInformation(
                                "🏁 SAGA_COMPLETE: All tiers completed successfully! " +
                                "CorrelationId={CorrelationId}, Sport={Sport}, Season={Season}, " +
                                "TotalDuration={TotalDuration}s, " +
                                "Tier1Events={Tier1Events}, Tier2Events={Tier2Events}, " +
                                "Tier3Events={Tier3Events}, Tier4Events={Tier4Events}",
                                context.Saga.CorrelationId,
                                context.Saga.Sport,
                                context.Saga.SeasonYear,
                                totalDuration,
                                context.Saga.SeasonCompletionEventsReceived,
                                context.Saga.VenueCompletionEventsReceived,
                                context.Saga.TeamSeasonCompletionEventsReceived,
                                context.Saga.AthleteSeasonCompletionEventsReceived);
                        })
                        .Finalize())
        );

        SetCompletedWhenFinalized();
    }
}

/// <summary>
/// Internal event to trigger sourcing of the next tier.
/// Consumed by ResourceIndexJob to start processing the specified tier.
/// </summary>
public record TriggerTierSourcing(
    Guid CorrelationId,
    Sport Sport,
    int SeasonYear,
    SourceDataProvider Provider,
    int Tier,
    string TierName
);
