using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Season)]
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Seasons)]
    public class SeasonDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<SeasonDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public SeasonDocumentProcessor(
            ILogger<SeasonDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IGenerateExternalRefIdentities externalRefIdentityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            // Step 1: Deserialize
            var externalProviderDto = command.Document.FromJson<EspnFootballSeasonDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
            {
                _logger.LogError("EspnFootballSeasonDto Ref is null or empty. {@Command}", command);
                return;
            }

            // Step 2: Map DTO -> Canonical Entity
            var mappedSeason = externalProviderDto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

            _logger.LogInformation("Mapped season: {@mappedSeason}", mappedSeason);

            // Step 3: Load existing from DB
            var existingSeason = await _dataContext.Seasons
                .Include(s => s.Phases)
                .Include(s => s.ExternalIds)
                .FirstOrDefaultAsync(s => s.Id == mappedSeason.Id);

            if (existingSeason != null)
            {
                await ProcessUpdateAsync(existingSeason, mappedSeason);
            }
            else
            {
                await ProcessNewEntity(command, externalProviderDto);
            }

            // Step 4: Save changes
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Finished processing season {SeasonId}", mappedSeason.Id);
        }

        private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnFootballSeasonDto dto)
        {
            var newEntity = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

            // capture the ActiveSeasonPhaseId if it exists (avoid circular reference)
            // EF Core cannot save circular reference Season ↔ ActivePhaseId in same SaveChanges call
            var currentSeasonPhaseId = newEntity.ActivePhaseId;

            newEntity.ActivePhaseId = null;
            await _dataContext.Seasons.AddAsync(newEntity);
            await _dataContext.SaveChangesAsync();

            // Re-attach the ActiveSeasonPhaseId after saving
            if (currentSeasonPhaseId.HasValue)
            {
                newEntity.ActivePhaseId = currentSeasonPhaseId.Value;
                await _dataContext.SaveChangesAsync();
            }

            _logger.LogInformation("Created new Season entity: {SeasonId}", newEntity.Id);
        }

        private Task ProcessUpdateAsync(Season existingSeason, Season mappedSeason)
        {
            // Update scalar properties
            existingSeason.Year = mappedSeason.Year;
            existingSeason.Name = mappedSeason.Name;
            existingSeason.StartDate = mappedSeason.StartDate;
            existingSeason.EndDate = mappedSeason.EndDate;
            existingSeason.ActivePhaseId = mappedSeason.ActivePhaseId;

            // Replace Phases wholesale
            _dataContext.SeasonPhases.RemoveRange(existingSeason.Phases);
            existingSeason.Phases.Clear();
            foreach (var phase in mappedSeason.Phases)
            {
                existingSeason.Phases.Add(phase);
            }

            // Replace ExternalIds wholesale
            _dataContext.SeasonExternalIds.RemoveRange(existingSeason.ExternalIds);
            existingSeason.ExternalIds.Clear();
            foreach (var extId in mappedSeason.ExternalIds)
            {
                existingSeason.ExternalIds.Add(extId);
            }

            _logger.LogInformation("Updated existing Season with Id {SeasonId}", existingSeason.Id);

            return Task.CompletedTask;
        }
    }
}
