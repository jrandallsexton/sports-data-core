using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitor)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionCompetitor)]
public class FootballEventCompetitionCompetitorDocumentProcessor<TDataContext> : EventCompetitionCompetitorDocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public FootballEventCompetitionCompetitorDocumentProcessor(
        ILogger<FootballEventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override CompetitionCompetitorBase CreateEntity(
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        Guid correlationId)
    {
        return dto.AsFootballEntity(
            competitionId,
            franchiseSeasonId,
            _externalRefIdentityGenerator,
            correlationId);
    }

    /// <summary>
    /// Sync CuratedRankCurrent on every pass, not just at creation. ESPN's
    /// curatedRank on an event competitor tracks the poll until kickoff and
    /// then freezes at the rank the team carried into the game (AP until CFP
    /// rankings publish, then CFP), so the latest fetch is always right.
    /// Previously it was written only by CreateEntity, which froze every
    /// competitor at its first-ingestion value: the 2026 NCAA schedule
    /// loaded before the preseason poll, leaving all 2026 rows at 99
    /// (unranked). A document without curatedRank leaves the stored value
    /// alone rather than erasing it.
    /// </summary>
    protected override Task ProcessSportSpecificCompetitorData(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitorBase entity)
    {
        if (entity is not FootballCompetitionCompetitor footballCompetitor
            || dto.EspnCuratedRank is null
            || footballCompetitor.CuratedRankCurrent == dto.EspnCuratedRank.Current)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "CompetitionCompetitor curated rank changed. CompetitorId={CompetitorId}, Rank: {OldRank} -> {NewRank}",
            footballCompetitor.Id,
            footballCompetitor.CuratedRankCurrent,
            dto.EspnCuratedRank.Current);

        footballCompetitor.CuratedRankCurrent = dto.EspnCuratedRank.Current;

        return Task.CompletedTask;
    }
}
