using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorStatistics)]
public class EventCompetitionCompetitorStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionCompetitorStatisticsDocumentProcessor(
        ILogger<EventCompetitionCompetitorStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, eventBus, identityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EventCompetitionCompetitorStatisticsDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionCompetitorId))
        {
            _logger.LogError("Invalid or missing CompetitionCompetitorId in ParentId.");
            return;
        }

        var competitionCompetitor = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.Id == competitionCompetitorId);

        if (competitionCompetitor is null)
        {
            // Check if dto.Ref is null before attempting to map
            if (dto.Ref is null)
            {
                _logger.LogWarning("CompetitionCompetitor not found and dto.Ref is null. Cannot resolve dependency. Skipping.");
                return;
            }

            var competitionCompetitorRef = EspnUriMapper.CompetitionCompetitorStatisticsRefToCompetitionCompetitorRef(dto.Ref);
            var competitionRef = EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(competitionCompetitorRef);
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            await PublishDependencyRequest<Guid>(
                command,
                new EspnLinkDto { Ref = competitionCompetitorRef },
                parentId: competitionIdentity.CanonicalId,
                DocumentType.EventCompetitionCompetitor);

            throw new ExternalDocumentNotSourcedException($"CompetitionCompetitor with Id {competitionCompetitorId} does not exist. Requested. Will retry.");
        }

        if (dto.Team?.Ref is null)
        {
            _logger.LogWarning("Team reference is null for competitor statistics. Skipping.");
            return;
        }

        // Resolve FranchiseSeason
        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);
        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(z => z.Value == franchiseSeasonIdentity.UrlHash));

        if (franchiseSeason is null)
        {
            _logger.LogError("FranchiseSeason not found for URL hash {Hash}", franchiseSeasonIdentity.UrlHash);
            return;
        }

        // Blow away any existing stats for this team/competition
        var existing = await _dataContext.CompetitionCompetitorStatistics
            .Include(x => x.Categories)
            .ThenInclude(x => x.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x =>
                x.CompetitionId == competitionCompetitor.Competition.Id &&
                x.FranchiseSeasonId == franchiseSeason.Id);

        if (existing is not null)
        {
            _dataContext.CompetitionCompetitorStatistics.Remove(existing);
            await _dataContext.SaveChangesAsync();
            _logger.LogInformation("Existing CompetitionCompetitorStatistic removed for FranchiseSeason {FranchiseSeasonId}, Competition {CompetitionId}", franchiseSeason.Id, competitionCompetitor.Competition.Id);
        }

        // Create new record
        var entity = dto.AsEntity(
            franchiseSeasonId: franchiseSeason.Id,
            competitionId: competitionCompetitor.Competition.Id,
            externalRefIdentityGenerator: _externalRefIdentityGenerator,
            correlationId: command.CorrelationId);

        await _dataContext.CompetitionCompetitorStatistics.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Inserted CompetitionCompetitorStatistic for FranchiseSeason {FranchiseSeasonId}, Competition {CompetitionId}", franchiseSeason.Id, competitionCompetitor.Competition.Id);
    }
}