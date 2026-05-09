using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionCompetitor)]
public class BaseballEventCompetitionCompetitorDocumentProcessor<TDataContext> : EventCompetitionCompetitorDocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public BaseballEventCompetitionCompetitorDocumentProcessor(
        ILogger<BaseballEventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    protected override EspnEventCompetitionCompetitorDto? DeserializeDto(string document)
        => document.FromJson<EspnBaseballEventCompetitionCompetitorDto>();

    protected override CompetitionCompetitorBase CreateEntity(
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        Guid correlationId)
    {
        return dto.AsBaseballEntity(
            competitionId,
            franchiseSeasonId,
            _externalRefIdentityGenerator,
            correlationId);
    }

    // MLB Probables ingestion. Each Probable has a hard FK to AthleteSeason
    // — if the athlete isn't sourced yet we request it and throw
    // ExternalDocumentNotSourcedException so Hangfire retries the document.
    // No partial Probable rows; an empty Probable is worthless on the
    // matchup card.
    //
    // See docs/competition-competitor-probables.md.
    protected override async Task ProcessSportSpecificCompetitorData(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitorBase entity)
    {
        if (dto is not EspnBaseballEventCompetitionCompetitorDto baseballDto
            || entity is not BaseballCompetitionCompetitor baseballCompetitor)
        {
            return;
        }

        if (baseballDto.Probables is null || baseballDto.Probables.Count == 0)
        {
            return;
        }

        foreach (var probableDto in baseballDto.Probables)
        {
            await UpsertProbable(command, baseballCompetitor.Id, probableDto);
        }
    }

    private async Task UpsertProbable(
        ProcessDocumentCommand command,
        Guid competitorId,
        EspnBaseballProbableDto probableDto)
    {
        if (string.IsNullOrWhiteSpace(probableDto.Name))
        {
            _logger.LogWarning(
                "Probable entry has no Name. Skipping. CompetitorId={CompetitorId}",
                competitorId);
            return;
        }

        if (probableDto.Athlete?.Ref is null)
        {
            _logger.LogWarning(
                "Probable entry has no athlete ref. Skipping. CompetitorId={CompetitorId}, Name={Name}",
                competitorId, probableDto.Name);
            return;
        }

        // Resolve AthleteSeason or fail-loud per the not-sourced pattern.
        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(probableDto.Athlete.Ref);
        var athleteSeasonExists = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .AnyAsync(x => x.Id == athleteSeasonIdentity.CanonicalId);

        if (!athleteSeasonExists)
        {
            await PublishDependencyRequest<string?>(
                command,
                new EspnLinkDto { Ref = probableDto.Athlete.Ref },
                parentId: null,
                DocumentType.AthleteSeason);

            throw new ExternalDocumentNotSourcedException(
                $"Probable AthleteSeason {athleteSeasonIdentity.CleanUrl} not sourced. Requested. Will retry. CompetitorId={competitorId}");
        }

        // Spawn the season-stats fetch for the probable's athlete so the
        // matchup card has ERA / W-L / K available downstream. Idempotent
        // by design — the AthleteSeasonStatistics processor upserts.
        // Parent is the AthleteSeason, not the competitor, since that's
        // the canonical owner of season-stat rows.
        await PublishChildDocumentRequest(
            command,
            probableDto.Statistics,
            athleteSeasonIdentity.CanonicalId,
            DocumentType.AthleteSeasonStatistics);

        // Deterministic Id from (competitorId, role-name) so reprocessing
        // updates the same row instead of inserting duplicates.
        var probableId = DeterministicGuid.Combine(
            "competitor-probable",
            competitorId.ToString(),
            probableDto.Name);

        var existing = await _dataContext.CompetitionCompetitorProbables
            .FirstOrDefaultAsync(x => x.Id == probableId);

        if (existing is null)
        {
            existing = new CompetitionCompetitorProbable
            {
                Id = probableId,
                CompetitionCompetitorId = competitorId,
                AthleteSeasonId = athleteSeasonIdentity.CanonicalId,
                EspnPlayerId = probableDto.PlayerId,
                Name = probableDto.Name,
                CreatedUtc = _dateTimeProvider.UtcNow(),
                CreatedBy = command.CorrelationId
            };
            await _dataContext.CompetitionCompetitorProbables.AddAsync(existing);
        }
        else
        {
            existing.ModifiedUtc = _dateTimeProvider.UtcNow();
            existing.ModifiedBy = command.CorrelationId;
        }

        // Name is the natural-key component of the deterministic Id, so
        // it never changes for a given row — no reassignment needed.
        existing.AthleteSeasonId = athleteSeasonIdentity.CanonicalId;
        existing.EspnPlayerId = probableDto.PlayerId;
        existing.DisplayName = probableDto.DisplayName;
        existing.ShortDisplayName = probableDto.ShortDisplayName;
        existing.Abbreviation = probableDto.Abbreviation;
    }
}
