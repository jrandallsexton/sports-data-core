﻿using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/probabilities?lang=en
    /// </summary>
    /// <typeparam name="TDataContext"></typeparam>
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionProbability)]
    public class EventCompetitionProbabilityDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionProbabilityDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionProbabilityDocumentProcessor(
            ILogger<EventCompetitionProbabilityDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
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
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var dto = command.Document.FromJson<EspnEventCompetitionProbabilityDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionProbabilityDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionProbabilityDto Ref is null or empty. {@Command}", command);
                return;
            }

            var competitionId = await _dataContext.TryResolveFromDtoRefAsync(
                dto.Competition,
                command.SourceDataProvider,
                () => _dataContext.Competitions.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (competitionId is null || competitionId == Guid.Empty)
            {
                _logger.LogWarning("No matching competition found for ref: {ref}", dto.Competition.Ref);
                return;
            }

            Guid? playId = null;

            if (!string.IsNullOrEmpty(dto.Play?.Ref?.ToString()))
            {
                playId = await _dataContext.TryResolveFromDtoRefAsync(
                    dto.Play,
                    command.SourceDataProvider,
                    () => _dataContext.Plays.Include(x => x.ExternalIds).AsNoTracking(),
                    _logger);
            }

            var entity = dto.AsEntity(
                _externalRefIdentityGenerator,
                competitionId.Value,
                playId,
                command.CorrelationId);

            await _dataContext.CompetitionProbabilities.AddAsync(entity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Persisted CompetitionProbability: {id}", entity.Id);
        }
    }
}
