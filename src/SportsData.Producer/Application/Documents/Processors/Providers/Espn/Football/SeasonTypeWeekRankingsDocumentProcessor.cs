using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
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
        private readonly IPublishEndpoint _publishEndpoint;

        public SeasonTypeWeekRankingsDocumentProcessor(
            ILogger<SeasonTypeWeekRankingsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IPublishEndpoint publishEndpoint)
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

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            if (!Guid.TryParse(command.ParentId, out var seasonWeekId))
            {
                _logger.LogError("SeasonWeekId could not be parsed");
                return;
            }

            var seasonWeek = await _dataContext.SeasonWeeks
                .Include(x => x.ExternalIds)
                .Include(x => x.Rankings)
                .ThenInclude(r => r.ExternalIds)
                .Where(x => x.Id == seasonWeekId)
                .FirstOrDefaultAsync();

            if (seasonWeek == null)
            {
                _logger.LogError("SeasonWeek not found.");
                throw new Exception("SeasonWeek not found");
            }

            var externalProviderDto = command.Document.FromJson<EspnFootballSeasonTypeWeekRankingsDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekRankingsDto. {@Command}", command);
                return;
            }

            var dtoIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);

            var ranking = seasonWeek.Rankings
                .FirstOrDefault(r => r.ExternalIds.Any(id => id.SourceUrlHash == dtoIdentity.UrlHash &&
                                                             id.Provider == command.SourceDataProvider));

            if (ranking is null)
            {
                await ProcessNewEntity(externalProviderDto, dtoIdentity, seasonWeekId, command);
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
            Dictionary<string, Guid> franchiseDictionary = new();

            foreach (var entry in dto.Ranks)
            {
                var teamIdentity = _externalRefIdentityGenerator.Generate(entry.Team.Ref);

                var franchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                    entry.Team,
                    command.SourceDataProvider,
                    () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                    _logger);

                if (franchiseSeasonId.HasValue)
                {
                    franchiseDictionary.Add(entry.Team.Ref.ToCleanUrl(), franchiseSeasonId.Value);
                }
                else
                {
                    _logger.LogError("Could not resolve FranchiseSeasonId for team ref: {TeamRef}", entry.Team.Ref);
                    throw new Exception($"Could not resolve FranchiseSeasonId for team ref: {entry.Team.Ref}");
                }
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


        private async Task ProcessExistingEntity()
        {
            _logger.LogError("Update detected. Not implemented");
            await Task.Delay(100);
        }
    }
}
