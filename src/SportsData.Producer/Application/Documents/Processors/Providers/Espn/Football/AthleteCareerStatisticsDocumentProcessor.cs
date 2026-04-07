using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.AthleteCareerStatistics)]
public class AthleteCareerStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public AthleteCareerStatisticsDocumentProcessor(
        ILogger<AthleteCareerStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var athleteId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.AthleteCareerStatisticsRefToAthleteRef);

        if (athleteId == null)
        {
            _logger.LogError("Unable to determine AthleteId from ParentId or URI");
            return;
        }

        var athleteIdValue = athleteId.Value;

        var athlete = await _dataContext.Athletes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == athleteIdValue);

        if (athlete is null)
        {
            _logger.LogError("Athlete not found: {AthleteId}", athleteIdValue);
            return;
        }

        var dto = command.Document.FromJson<EspnAthleteCareerStatisticsDto>();

        if (dto is null)
        {
            _logger.LogError("DTO is null for AthleteCareerStatistics processing. ParentId: {ParentId}", command.ParentId);
            return;
        }

        if (dto.Ref == null)
        {
            _logger.LogError("AthleteCareerStatistics DTO missing $ref. ParentId: {ParentId}", command.ParentId);
            return;
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        // ESPN replaces statistics wholesale — delete existing then insert fresh.
        var existing = await _dataContext.AthleteCareerStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Removing existing AthleteCareerStatistic {Id} for replacement",
                identity.CanonicalId);
            _dataContext.AthleteCareerStatistics.Remove(existing);
            await _dataContext.SaveChangesAsync();
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            athleteIdValue,
            command.CorrelationId);

        await _dataContext.AthleteCareerStatistics.AddAsync(entity);

        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            // Another pod won the race and already inserted this entity — treat as idempotent success.
            _logger.LogWarning(
                "Duplicate key on AthleteCareerStatistic insert — another process already created it. " +
                "Id={Id}, CorrelationId={CorrelationId}",
                entity.Id, command.CorrelationId);

            _dataContext.Entry(entity).State = EntityState.Detached;
            foreach (var category in entity.Categories.ToList())
            {
                foreach (var stat in category.Stats.ToList())
                    _dataContext.Entry(stat).State = EntityState.Detached;
                _dataContext.Entry(category).State = EntityState.Detached;
            }

            return;
        }

        _logger.LogInformation(
            "Successfully processed AthleteCareerStatistics {Id} for Athlete {AthleteId} with {CategoryCount} categories and {StatCount} total stats",
            entity.Id,
            athleteIdValue,
            entity.Categories.Count,
            entity.Categories.Sum(c => c.Stats.Count));
    }
}
