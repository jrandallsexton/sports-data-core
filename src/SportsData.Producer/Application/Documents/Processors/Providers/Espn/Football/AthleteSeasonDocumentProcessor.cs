using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
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
            _logger.LogInformation("Processing AthleteSeason with {@Command}", command);
            await ProcessInternal(command);
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

        // Construct the canonical athlete ref from the known pattern
        var baseRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = _externalRefIdentityGenerator.Generate(baseRef);

        var athleteId = await _dataContext.AthleteExternalIds
            .AsNoTracking()
            .Where(x => x.Provider == command.SourceDataProvider && x.SourceUrlHash == athleteIdentity.UrlHash)
            .Select(x => x.AthleteId)
            .FirstOrDefaultAsync();

        if (athleteId == Guid.Empty)
        {
            _logger.LogWarning("Athlete not found for hash: {Hash}. Raising DocumentRequested.", athleteIdentity.UrlHash);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: athleteIdentity.CanonicalId.ToString(),
                ParentId: null,
                Uri: new Uri(baseRef),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.Athlete,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
            ));

            return;
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
            athleteId,
            command.CorrelationId);

        await _dataContext.AthleteSeasons.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully created AthleteSeason {Id} for Athlete {AthleteId}", entity.Id, athleteId);
    }

    private async Task<Guid> TryResolveFranchiseSeasonIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Team?.Ref is null)
            return Guid.Empty;

        var identity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);

        var id = await _dataContext.FranchiseSeasonExternalIds
            .AsNoTracking()
            .Where(x => x.Provider == command.SourceDataProvider && x.SourceUrlHash == identity.UrlHash)
            .Select(x => x.FranchiseSeasonId)
            .FirstOrDefaultAsync();

        if (id == Guid.Empty)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: identity.CanonicalId.ToString(),
                ParentId: null,
                Uri: dto.Team.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.TeamSeason,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
            ));
        }

        return id;
    }

    private async Task<Guid> TryResolvePositionIdAsync(EspnAthleteSeasonDto dto, ProcessDocumentCommand command)
    {
        if (dto.Position?.Ref is null)
            return Guid.Empty;

        var identity = _externalRefIdentityGenerator.Generate(dto.Position.Ref);

        var id = await _dataContext.AthletePositionExternalIds
            .AsNoTracking()
            .Where(x => x.Provider == command.SourceDataProvider && x.SourceUrlHash == identity.UrlHash)
            .Select(x => x.AthletePositionId)
            .FirstOrDefaultAsync();

        if (id == Guid.Empty)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: identity.CanonicalId.ToString(),
                ParentId: null,
                Uri: dto.Position.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.AthletePosition,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.AthleteSeasonDocumentProcessor
            ));
        }

        return id;
    }
}
