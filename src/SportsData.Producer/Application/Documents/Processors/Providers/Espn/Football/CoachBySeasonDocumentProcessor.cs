using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachSeason)]
    public class CoachBySeasonDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<CoachBySeasonDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public CoachBySeasonDocumentProcessor(
            ILogger<CoachBySeasonDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus publishEndpoint,
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
                _logger.LogInformation("Began processing Coach with {@Command}", command);
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
            var dto = command.Document.FromJson<EspnCoachSeasonDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnCoachDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("EspnCoachDto Ref is null or empty. {@Command}", command);
                return;
            }

            var coachSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

            var coachSeason = await _dataContext.CoachSeasons
                .Where(x => x.Id == coachSeasonIdentity.CanonicalId)
                .FirstOrDefaultAsync();

            if (coachSeason is null)
            {
                await ProcessNewEntity(command, coachSeasonIdentity, dto);
            }
            else
            {
                await ProcessUpdate(command, dto);
            }
        }

        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            ExternalRefIdentity coachSeasonIdentity,
            EspnCoachSeasonDto dto)
        {
            var coachIdentity = _externalRefIdentityGenerator.Generate(dto.Person.Ref);

            var coach = await _dataContext.Coaches
                .FirstOrDefaultAsync(x => x.Id == coachIdentity.CanonicalId);

            if (coach is null)
            {
                _logger.LogWarning("Coach not found. Will request sourcing and retry");
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: coachIdentity.UrlHash,
                    ParentId: null,
                    Uri: dto.Person.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.Coach,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.CoachSeasonDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                _logger.LogWarning("Coach not found. Will request sourcing and retry. {@Identity}", coachIdentity);

                throw new ExternalDocumentNotSourcedException("Coach not found. Will request sourcing and retry.");
            }

            var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);

            var franchiseSeason = await _dataContext.FranchiseSeasons
                .FirstOrDefaultAsync(x => x.Id == franchiseSeasonIdentity.CanonicalId);

            if (franchiseSeason is null)
            {
                _logger.LogWarning("FranchiseSeason not found. Will request sourcing and retry");
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: coachIdentity.UrlHash,
                    ParentId: null,
                    Uri: dto.Team.Ref.ToCleanUri(),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.TeamSeason,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.CoachSeasonDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                _logger.LogWarning("FranchiseSeason not found. Will request sourcing and retry. {@Identity}", franchiseSeasonIdentity);

                throw new ExternalDocumentNotSourcedException("FranchiseSeason not found. Will request sourcing and retry.");
            }

            var newEntity = new CoachSeason()
            {
                Id = coachSeasonIdentity.CanonicalId,
                CoachId = coach.Id,
                FranchiseSeasonId = franchiseSeason.Id,
                CreatedBy = command.CorrelationId
            };

            // TODO: Determine if there is anything else to source ... Records collection?

            await _dataContext.CoachSeasons.AddAsync(newEntity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created new CoachSeason entity: {CoachId}", newEntity.Id);
        }

        private async Task ProcessUpdate(ProcessDocumentCommand command, EspnCoachSeasonDto dto)
        {
            await Task.Delay(100);
            _logger.LogWarning("Update detected; not implemented");
        }
    }
}