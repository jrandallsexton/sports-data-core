using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Test;

/// <summary>
/// Test document processor that validates:
/// 1. Generic processor with BaseDataContext constraint works with concrete types
/// 2. Outbox pattern functions correctly without OutboxPing
/// 3. Factory creates processor with correct DbContext type (FootballDataContext, etc.)
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.OutboxTest)]
public class OutboxTestDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseDataContext
{
    public OutboxTestDocumentProcessor(
        ILogger<OutboxTestDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, bus, externalRefIdentityGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["TestId"] = Guid.NewGuid()
        }))
        {
            _logger.LogInformation(
                "OUTBOX_TEST PROCESSOR (BaseDataContext): Processing with DbContext type: {DbContextType}, Generic parameter: {GenericType}",
                _dataContext.GetType().Name,
                typeof(TDataContext).Name);

            // Publish OutboxTestEvent (should go through outbox)
            var evt = new OutboxTestEvent(
                Message: $"BaseDataContext test: Published from {nameof(OutboxTestDocumentProcessor<TDataContext>)} with {_dataContext.GetType().Name}",
                ContextType: $"{typeof(TDataContext).Name} (runtime: {_dataContext.GetType().Name})",
                TestId: Guid.Parse(command.ParentId ?? Guid.NewGuid().ToString()),
                PublishedUtc: DateTime.UtcNow
            );

            await _publishEndpoint.Publish(evt);

            // Cascade test: Publish DocumentCreated event for TeamSport processor
            // This validates the ENTIRE inheritance chain works
            _logger.LogInformation("Publishing cascade test: DocumentCreated for OutboxTestTeamSport");
            
            var teamSportTestEvent = new SportsData.Core.Eventing.Events.Documents.DocumentCreated(
                Id: Guid.NewGuid().ToString(),
                ParentId: command.ParentId,
                Name: "OutboxTestTeamSport",
                Ref: new Uri("http://test.com/outbox-test-teamsport"),
                SourceRef: new Uri("http://test.com/outbox-test-teamsport"),
                DocumentJson: "{}",
                SourceUrlHash: "test-hash-teamsport",
                Sport: Sport.FootballNcaa,
                SeasonYear: 2024,
                DocumentType: DocumentType.OutboxTestTeamSport,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: Guid.NewGuid(),
                AttemptCount: 0,
                IncludeLinkedDocumentTypes: null
            );

            await _publishEndpoint.Publish(teamSportTestEvent);

            // Save changes (should flush outbox WITHOUT OutboxPing)
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "OUTBOX_TEST PROCESSOR (BaseDataContext): Completed. Events published via {DbContextType}",
                _dataContext.GetType().Name);
        }
    }
}
