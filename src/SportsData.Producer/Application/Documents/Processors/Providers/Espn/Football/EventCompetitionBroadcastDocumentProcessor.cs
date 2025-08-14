using MassTransit;

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
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionBroadcast)]
    public class EventCompetitionBroadcastDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionBroadcastDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionBroadcastDocumentProcessor(
            ILogger<EventCompetitionBroadcastDocumentProcessor<TDataContext>> logger,
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
                _logger.LogInformation("Processing EventCompetitionBroadcastDocument with {@Command}", command);
                try
                {
                    await ProcessInternal(command);
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
            var externalDto = command.Document.FromJson<EspnEventCompetitionBroadcastDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionBroadcastDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(command.ParentId))
            {
                _logger.LogError("ParentId not provided. Cannot process broadcast for null CompetitionId");
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("Invalid ParentId format for CompetitionId. Cannot parse to Guid.");
                return;
            }

            var competition = await _dataContext.Competitions
                .Include(x => x.Broadcasts)
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition is null)
            {
                _logger.LogError("Competition not found.");
                throw new InvalidOperationException($"Competition with Id {competitionId} not found.");
            }

            var existingKeys = new HashSet<string>(
                competition.Broadcasts.Select(x => $"{x.TypeId}|{x.Channel}|{x.Slug}".ToLowerInvariant())
            );

            var newBroadcasts = externalDto.Items
                .Where(item =>
                {
                    var key = $"{item.Type.Id}|{item.Channel}|{item.Slug}".ToLowerInvariant();
                    return !existingKeys.Contains(key);
                })
                .Select(item => item.AsEntity(competition.Id))
                .ToList();

            if (newBroadcasts.Count > 0)
            {
                _dataContext.Broadcasts.AddRange(newBroadcasts);
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
