using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Test;

/// <summary>
/// Test document processor that validates:
/// 1. Generic processor with TeamSportDataContext constraint works with concrete types
/// 2. Factory creates processor with FootballDataContext (which inherits from TeamSportDataContext)
/// 3. Outbox pattern functions correctly for the full inheritance chain
/// 
/// Inheritance chain: FootballDataContext ? TeamSportDataContext ? BaseDataContext
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.OutboxTestTeamSport)]
public class OutboxTestTeamSportDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public OutboxTestTeamSportDocumentProcessor(
        ILogger<OutboxTestTeamSportDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, bus, externalRefIdentityGenerator, refs)
    {
    }

    protected override Task ProcessInternal(ProcessDocumentCommand command) => Task.CompletedTask;

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["TestId"] = command.ParentId ?? Guid.NewGuid().ToString()
        }))
        {
            _logger.LogInformation(
                "TEAMSPORT TEST PROCESSOR: Processing with DbContext type: {DbContextType}, Generic parameter: {GenericType}",
                _dataContext.GetType().Name,
                typeof(TDataContext).Name);

            // Validate the inheritance chain
            var isFootball = _dataContext is FootballDataContext;
            var isTeamSport = _dataContext is TeamSportDataContext;
            var isBase = _dataContext is BaseDataContext;

            _logger.LogInformation(
                "Inheritance validation: isFootballDataContext={IsFootball}, isTeamSportDataContext={IsTeamSport}, isBaseDataContext={IsBase}",
                isFootball, isTeamSport, isBase);

            // Publish event (should go through outbox)
            var evt = new OutboxTestEvent(
                Message: $"TeamSport test: {typeof(TDataContext).Name} ? {_dataContext.GetType().Name}. Inheritance chain validated!",
                ContextType: $"TeamSport constraint (runtime: {_dataContext.GetType().Name})",
                TestId: Guid.Parse(command.ParentId ?? Guid.NewGuid().ToString()),
                PublishedUtc: DateTime.UtcNow
            );

            await _publishEndpoint.Publish(evt);

            // Save changes (should flush outbox WITHOUT OutboxPing)
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "TEAMSPORT TEST PROCESSOR: Completed. Inheritance chain: FootballDataContext={IsFootball}, TeamSportDataContext={IsTeamSport}, BaseDataContext={IsBase}",
                isFootball, isTeamSport, isBase);
        }
    }
}
