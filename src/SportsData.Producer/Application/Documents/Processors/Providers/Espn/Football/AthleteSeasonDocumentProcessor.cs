using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeason)]
public class AthleteSeasonDocumentProcessor : IProcessDocuments
{
    private readonly ILogger<AthleteSeasonDocumentProcessor> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public AthleteSeasonDocumentProcessor(
        ILogger<AthleteSeasonDocumentProcessor> logger,
        FootballDataContext dataContext,
        IPublishEndpoint publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Processing EventDocument with {@Command}", command);
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnAthleteSeasonDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnAthleteSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            _logger.LogError("AthleteSeasonDto missing Id. {@Command}", command);
            return;
        }

        var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(dto.Ref);
        var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

        var athlete = await _dataContext.Athletes
            .Include(x => x.Seasons)
            .Where(x => x.Id == athleteIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (athlete is null)
        {
            _logger.LogWarning("Athlete not found for hash: {Hash}. Raising DocumentRequested.", athleteIdentity.UrlHash);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: athleteIdentity.CanonicalId.ToString(),
                ParentId: null,
                Uri: athleteRef,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.Athlete,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
            ));
            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            throw new ExternalDocumentNotSourcedException(
                $"Athlete not found for {dto.Ref} in command {command.CorrelationId}");
        }

        var franchiseSeasonId = await TryResolveFranchiseSeasonIdAsync(dto, command);
        if (franchiseSeasonId == Guid.Empty)
        {
            _logger.LogError("Could not resolve FranchiseSeasonId for Team.Ref: {Ref}", dto.Team?.Ref?.ToString() ?? "null");
            return;
        }

        var positionId = await TryResolvePositionIdAsync(dto, command);
        if (positionId == Guid.Empty)
        {
            _logger.LogError("Could not resolve PositionId for Position.Ref: {Ref}", dto.Position?.Ref?.ToString() ?? "null");
            return;
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchiseSeasonId,
            positionId,
            athlete.Id,
            command.CorrelationId);

        await _dataContext.AthleteSeasons.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully created AthleteSeason {Id} for Athlete {AthleteId}", entity.Id, athlete.Id);
    }

    private async Task<Guid> TryResolveFranchiseSeasonIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Team?.Ref is null)
            return Guid.Empty;

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);

        var franchise = await _dataContext.Franchises
            .FirstOrDefaultAsync(x => x.Id == franchiseSeasonIdentity.CanonicalId);

        if (franchise is not null)
            return franchise.Id;

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: franchiseSeasonIdentity.CanonicalId.ToString(),
            ParentId: null,
            Uri: dto.Team.Ref,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: DocumentType.TeamSeason,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
        ));
        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        await _dataContext.SaveChangesAsync();

        throw new ExternalDocumentNotSourcedException(
            $"Franchise season not found for {dto.Team.Ref} in command {command.CorrelationId}");
    }

    private async Task<Guid> TryResolvePositionIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Position?.Ref is null)
            return Guid.Empty;

        var positionIdentity = _externalRefIdentityGenerator.Generate(dto.Position.Ref);

        var position = await _dataContext.AthletePositions
            .FirstOrDefaultAsync(x => x.Id == positionIdentity.CanonicalId);

        if (position is not null)
            return position.Id;

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: positionIdentity.CanonicalId.ToString(),
            ParentId: null,
            Uri: dto.Position.Ref,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: DocumentType.AthletePosition,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
        ));
        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        await _dataContext.SaveChangesAsync();

        throw new ExternalDocumentNotSourcedException(
            $"Position not found for {dto.Position.Ref} in command {command.CorrelationId}");
    }
}
