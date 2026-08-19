using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeasonStatistics)]
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.AthleteSeasonStatistics)]
    [DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.AthleteSeasonStatistics)]
    public class AthleteSeasonStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
        where TDataContext : TeamSportDataContext
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

            // Season guard: ESPN hands out the PRIOR season's statistics ref on a
            // new season's athlete document until the new season has data (e.g. a
            // seasons/2026 athlete doc links seasons/2025/.../statistics). ParentId
            // is the spawning roster row, so trusting it blindly files last
            // season's numbers under the new season. Attach by the season in the
            // doc's own ref instead.
            if (EspnUriMapper.TryExtractSeasonYear(dto.Ref, out var refSeasonYear))
            {
                var parentSeasonYear = await _dataContext.FranchiseSeasons
                    .AsNoTracking()
                    .Where(f => f.Id == athleteSeason.FranchiseSeasonId)
                    .Select(f => (int?)f.SeasonYear)
                    .FirstOrDefaultAsync();

                if (parentSeasonYear.HasValue && parentSeasonYear.Value != refSeasonYear)
                {
                    var redirected = await _dataContext.AthleteSeasons
                        .AsNoTracking()
                        .Where(a => a.AthleteId == athleteSeason.AthleteId && a.Id != athleteSeasonIdValue)
                        .Join(
                            _dataContext.FranchiseSeasons.Where(f => f.SeasonYear == refSeasonYear),
                            a => a.FranchiseSeasonId,
                            f => f.Id,
                            (a, f) => new { a.Id, a.CreatedUtc })
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefaultAsync();

                    if (redirected is null)
                    {
                        _logger.LogWarning(
                            "AthleteSeasonStatistics ref is for season {RefSeasonYear} but spawning AthleteSeason {AthleteSeasonId} is season {ParentSeasonYear} and the athlete has no roster row for the ref's season. Skipping to avoid mislabeled stats. AthleteId={AthleteId}, Ref={Ref}",
                            refSeasonYear, athleteSeasonIdValue, parentSeasonYear.Value, athleteSeason.AthleteId, dto.Ref);
                        return;
                    }

                    _logger.LogInformation(
                        "Redirecting AthleteSeasonStatistics from spawning AthleteSeason {SpawningId} (season {ParentSeasonYear}) to AthleteSeason {TargetId} matching the ref's season {RefSeasonYear}",
                        athleteSeasonIdValue, parentSeasonYear.Value, redirected.Id, refSeasonYear);

                    athleteSeasonIdValue = redirected.Id;
                }
            }
            else
            {
                _logger.LogWarning(
                    "Unable to extract season year from AthleteSeasonStatistics ref; attaching to spawning AthleteSeason {AthleteSeasonId} unverified. Ref={Ref}",
                    athleteSeasonIdValue, dto.Ref);
            }

            var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

            // ESPN replaces statistics wholesale — delete existing then insert fresh.
            var existing = await _dataContext.AthleteSeasonStatistics
                .Include(x => x.Categories)
                    .ThenInclude(c => c.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Removing existing AthleteSeasonStatistic {Id} for replacement",
                    identity.CanonicalId);
                _dataContext.AthleteSeasonStatistics.Remove(existing);
                await _dataContext.SaveChangesAsync();
            }

            var entity = dto.AsEntity(
                _externalRefIdentityGenerator,
                athleteSeasonIdValue,
                command.CorrelationId);

            await _dataContext.AthleteSeasonStatistics.AddAsync(entity);

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // Another pod won the race and already inserted this entity — treat as idempotent success.
                _logger.LogWarning(
                    "Duplicate key on AthleteSeasonStatistic insert — another process already created it. " +
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
                "Successfully processed AthleteSeasonStatistics {Id} for AthleteSeason {AthleteSeasonId} with {CategoryCount} categories and {StatCount} total stats",
                entity.Id,
                athleteSeasonIdValue,
                entity.Categories.Count,
                entity.Categories.Sum(c => c.Stats.Count));
        }
    }
}
