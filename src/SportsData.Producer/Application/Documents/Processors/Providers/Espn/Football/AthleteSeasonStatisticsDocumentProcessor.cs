using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using SportsData.Core.Infrastructure.Refs;
using SportsData.Core.Infrastructure.DataSources.Espn;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeasonStatistics)]
    public class AthleteSeasonStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
        where TDataContext : FootballDataContext
    {
        public AthleteSeasonStatisticsDocumentProcessor(
            ILogger<AthleteSeasonStatisticsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus publishEndpoint,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
            : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
        {
        }

        protected override async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var athleteSeasonId = TryGetOrDeriveParentId(
                command,
                EspnUriMapper.AthleteSeasonStatisticsRefToAthleteSeasonRef);

            if (athleteSeasonId == null)
            {
                _logger.LogError("Unable to determine AthleteSeasonId from ParentId or URI");
                return;
            }

            var athleteSeasonIdValue = athleteSeasonId.Value;

            var athleteSeason = await _dataContext.AthleteSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == athleteSeasonIdValue);

            if (athleteSeason is null)
            {
                _logger.LogError("AthleteSeason not found: {AthleteSeasonId}", athleteSeasonIdValue);
                return;
            }

            var dto = command.Document.FromJson<EspnAthleteSeasonStatisticsDto>();

            if (dto is null)
            {
                _logger.LogError("DTO is null for AthleteSeasonStatistics processing. ParentId: {ParentId}", command.ParentId);
                return;
            }

            if (dto.Ref == null)
            {
                _logger.LogError("AthleteSeasonStatistics DTO missing $ref. ParentId: {ParentId}", command.ParentId);
                return;
            }

            var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

            // ESPN replaces statistics wholesale, so remove existing if present
            var existing = await _dataContext.AthleteSeasonStatistics
                .Include(x => x.Categories)
                    .ThenInclude(c => c.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

            if (existing is not null)
            {
                _logger.LogInformation("Removing existing AthleteSeasonStatistic {Id} for replacement", existing.Id);
                _dataContext.AthleteSeasonStatistics.Remove(existing);
            }

            var entity = dto.AsEntity(
                _externalRefIdentityGenerator,
                athleteSeasonIdValue,
                command.CorrelationId);

            await _dataContext.AthleteSeasonStatistics.AddAsync(entity);

            // Save both remove and add in a single transaction
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully processed AthleteSeasonStatistics {Id} for AthleteSeason {AthleteSeasonId} with {CategoryCount} categories and {StatCount} total stats",
                entity.Id,
                athleteSeasonIdValue,
                entity.Categories.Count,
                entity.Categories.Sum(c => c.Stats.Count));
        }
    }
}

