using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Stub processor for TeamSeasonNote documents.
/// ESPN currently returns empty collections for all TeamSeasonNote refs.
/// Full implementation will follow the AthleteSeasonNoteDocumentProcessor pattern
/// once real data is available.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonNote)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.TeamSeasonNote)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.TeamSeasonNote)]
public class TeamSeasonNoteDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonNoteDocumentProcessor(
        ILogger<TeamSeasonNoteDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override Task ProcessInternal(ProcessDocumentCommand command)
    {
        _logger.LogInformation("TeamSeasonNote document received. No processing implemented yet (ESPN returns empty collections).");
        return Task.CompletedTask;
    }
}
