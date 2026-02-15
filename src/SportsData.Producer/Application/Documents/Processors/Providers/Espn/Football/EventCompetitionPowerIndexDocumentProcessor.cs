using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPowerIndex)]
public class EventCompetitionPowerIndexDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionPowerIndexDocumentProcessor(
        ILogger<EventCompetitionPowerIndexDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, identityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionPowerIndexDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionPowerIndexDto.");
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPowerIndexDto Ref is null or empty.");
            return;
        }

        // Resolve Competition
        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid or missing Competition ID in ParentId. ParentId={ParentId}", command.ParentId);
            throw new InvalidOperationException("Missing or invalid parent ID");
        }

        var competition = await _dataContext.Competitions
            .Include(x => x.PowerIndexes)
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId);
            throw new InvalidOperationException($"Competition with ID {competitionId} does not exist.");
        }

        // Resolve FranchiseSeasonId from Team ref
        var franchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            dto.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        if (franchiseSeasonId is null)
        {
            if (dto.Team?.Ref is null)
            {
                _logger.LogWarning("Team reference is null for power index. Skipping.");
                return;
            }

            var teamHash = HashProvider.GenerateHashFromUri(dto.Team.Ref);

            _logger.LogWarning("FranchiseSeason not found, publishing sourcing request. Hash={Hash}", teamHash);

            await PublishChildDocumentRequest(
                command,
                dto.Team,
                parentId: string.Empty,
                DocumentType.TeamSeason);

            await _dataContext.SaveChangesAsync();

            throw new InvalidOperationException("FranchiseSeason not found.");
        }

        var newIndexCount = 0;
        var discoveredIndexNames = new List<string>();

        foreach (var stat in dto.Stats)
        {
            var powerIndexName = stat.Name.Trim().ToLowerInvariant();

            var powerIndex = await _dataContext.PowerIndexes
                .FirstOrDefaultAsync(p => p.Name.ToLower() == powerIndexName.ToLower());

            if (powerIndex is null)
            {
                _logger.LogInformation("Discovered new PowerIndex. Name={Name}, DisplayName={DisplayName}", 
                    stat.Name,
                    stat.DisplayName);

                powerIndex = new PowerIndex()
                {
                    Id = Guid.NewGuid(),
                    Name = stat.Name,
                    DisplayName = stat.DisplayName,
                    Description = stat.Description,
                    Abbreviation = stat.Abbreviation,
                    CreatedBy = command.CorrelationId
                };
                await _dataContext.PowerIndexes.AddAsync(powerIndex);
                newIndexCount++;
                discoveredIndexNames.Add(stat.Name);
            }

            var index = stat.AsEntity(
                _externalRefIdentityGenerator,
                dto.Ref,
                powerIndex.Id,
                competitionId,
                franchiseSeasonId.Value,
                command.CorrelationId);

            await _dataContext.CompetitionPowerIndexes.AddAsync(index);
        }

        await _dataContext.SaveChangesAsync();

        if (newIndexCount > 0)
        {
            _logger.LogInformation("Discovered {Count} new PowerIndexes. CompetitionId={CompId}, NewIndexes={Indexes}", 
                newIndexCount,
                competitionId,
                string.Join(", ", discoveredIndexNames));
        }

        _logger.LogInformation("Persisted CompetitionPowerIndexes. CompetitionId={CompId}, FranchiseSeasonId={TeamId}, IndexCount={Count}", 
            competitionId,
            franchiseSeasonId.Value,
            dto.Stats.Count());
    }
}