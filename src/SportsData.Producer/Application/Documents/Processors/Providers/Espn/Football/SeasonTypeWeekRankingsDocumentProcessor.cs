using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeekRankings)]
    public class SeasonTypeWeekRankingsDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<SeasonTypeWeekRankingsDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IEventBus _publishEndpoint;

        public SeasonTypeWeekRankingsDocumentProcessor(
            ILogger<SeasonTypeWeekRankingsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IEventBus publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Began with {@command}", command);

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
            var dto = command.Document.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekRankingsDto. {@Command}", command);
                return;
            }

            if (command.Season is null)
            {
                _logger.LogError("Command does not contain a valid Season. {@Command}", command);
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var seasonWeekId))
            {
                _logger.LogWarning("SeasonWeekId not on command.ParentId. Attempting to get from dto.");
                // can we get it from the DTO?
                var seasonTypeWeekRef = dto.Season.Type.Week.Ref;
                var seasonTypeWeekIdentity = _externalRefIdentityGenerator.Generate(seasonTypeWeekRef);

                seasonWeekId = seasonTypeWeekIdentity.CanonicalId;
            }

            var seasonWeek = await _dataContext.SeasonWeeks
                .Include(x => x.ExternalIds)
                .Include(x => x.Rankings)
                .ThenInclude(r => r.ExternalIds)
                .Where(x => x.Id == seasonWeekId)
                .FirstOrDefaultAsync();

            if (seasonWeek == null)
            {
                var seasonPhaseIdentity = _externalRefIdentityGenerator.Generate(dto.Season.Type.Ref);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: HashProvider.GenerateHashFromUri(dto.Season.Type.Week.Ref),
                    ParentId: seasonPhaseIdentity.CanonicalId.ToString(),
                    Uri: dto.Season.Type.Week.Ref,
                    Sport: Sport.FootballNcaa,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.SeasonTypeWeek,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor
                ));

                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                _logger.LogError("SeasonWeek not found. Sourcing requested. Will retry.");
                throw new ExternalDocumentNotSourcedException("SeasonWeek not found. Sourcing requested. Will retry.");
            }

            var dtoIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

            var ranking = seasonWeek.Rankings
                .FirstOrDefault(r => r.ExternalIds.Any(id => id.SourceUrlHash == dtoIdentity.UrlHash &&
                                                             id.Provider == command.SourceDataProvider));

            if (ranking is null)
            {
                await ProcessNewEntity(dto, dtoIdentity, seasonWeekId, command);
            }
            else 
            {
                await ProcessExistingEntity();
            }
        }

        private async Task ProcessNewEntity(
            EspnFootballSeasonTypeWeekRankingsDto dto,
            ExternalRefIdentity dtoIdentity,
            Guid seasonWeekId,
            ProcessDocumentCommand command)
        {
            // We need to create a mapping of the Team's season ref to the FranchiseSeasonId
            var (franchiseDictionary, missingFranchiseSeasons) = await ResolveFranchiseSeasonIdsAsync(
                dto,
                _externalRefIdentityGenerator,
                _dataContext,
                command,
                _logger);

            if (missingFranchiseSeasons.Any())
            {
                foreach (var missing in missingFranchiseSeasons)
                {
                    _logger.LogError("Missing FranchiseSeason for Team Ref {TeamRef} with expected URI {Uri}",
                        missing.Key, missing.Value);

                    var franchiseRef = EspnUriMapper.TeamSeasonToFranchiseRef(missing.Value);
                    var franchiseId = _externalRefIdentityGenerator.Generate(franchiseRef).CanonicalId;

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: missing.Key.ToString(),
                        ParentId: franchiseId.ToString(),
                        Uri: missing.Value,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: command.Season!.Value,
                        DocumentType: DocumentType.TeamSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor
                    ));
                }

                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new Exception($"{missingFranchiseSeasons.Count} FranchiseSeasons could not be resolved. Sourcing requested. Will retry this job.");
            }

            // Create the entity from the DTO
            var entity = dto.AsEntity(
                seasonWeekId,
                _externalRefIdentityGenerator,
                franchiseDictionary,
                command.CorrelationId);

            // Add to EF and save
            await _dataContext.SeasonRankings.AddAsync(entity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created SeasonRanking entity {@SeasonRankingId}", entity.Id);
        }

        private static async Task<(Dictionary<string, Guid> franchiseDictionary, Dictionary<Guid, Uri> missingFranchiseSeasons)>
            ResolveFranchiseSeasonIdsAsync(
                EspnFootballSeasonTypeWeekRankingsDto dto,
                IGenerateExternalRefIdentities externalRefIdentityGenerator,
                TDataContext dataContext,
                ProcessDocumentCommand command,
                ILogger logger)
        {
            var franchiseDictionary = new Dictionary<string, Guid>();
            var missingFranchiseSeasons = new Dictionary<Guid, Uri>();

            foreach (var entry in dto.Ranks)
            {
                var teamRef = entry.Team?.Ref;
                if (teamRef is null)
                    continue;

                var teamIdentity = externalRefIdentityGenerator.Generate(teamRef);

                var franchiseSeasonId = await dataContext.TryResolveFromDtoRefAsync(
                    entry.Team!,
                    command.SourceDataProvider,
                    () => dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                    logger);

                if (franchiseSeasonId.HasValue)
                {
                    franchiseDictionary.TryAdd(teamRef.ToCleanUrl(), franchiseSeasonId.Value);
                }
                else
                {
                    missingFranchiseSeasons.TryAdd(teamIdentity.CanonicalId, teamRef);
                }
            }

            if (dto.Others is not null)
            {
                foreach (var entry in dto.Others)
                {
                    var teamRef = entry.Team?.Ref;
                    if (teamRef is null)
                        continue;

                    var teamIdentity = externalRefIdentityGenerator.Generate(teamRef);

                    var franchiseSeasonId = await dataContext.TryResolveFromDtoRefAsync(
                        entry.Team!,
                        command.SourceDataProvider,
                        () => dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                        logger);

                    if (franchiseSeasonId.HasValue)
                    {
                        franchiseDictionary.TryAdd(teamRef.ToCleanUrl(), franchiseSeasonId.Value);
                    }
                    else
                    {
                        missingFranchiseSeasons.TryAdd(teamIdentity.CanonicalId, teamRef);
                    }
                }
            }

            return (franchiseDictionary, missingFranchiseSeasons);
        }


        private async Task ProcessExistingEntity()
        {
            _logger.LogError("Update detected. Not implemented");
            await Task.Delay(100);
        }
    }
}
