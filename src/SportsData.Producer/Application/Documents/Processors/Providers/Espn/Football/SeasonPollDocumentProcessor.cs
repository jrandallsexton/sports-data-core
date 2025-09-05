﻿using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonPoll)]
    public class SeasonPollDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<SeasonPollDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IEventBus _publishEndpoint;

        public SeasonPollDocumentProcessor(
            ILogger<SeasonPollDocumentProcessor<TDataContext>> logger,
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                    throw;
                }
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var dto = command.Document.FromJson<EspnFootballSeasonRankingDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonRankingDto. {@Command}", command);
                return;
            }

            var dtoIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

            // does this poll already exist?
            var existingPoll = await _dataContext.SeasonPolls
                .Where(x => x.Id == dtoIdentity.CanonicalId)
                .FirstOrDefaultAsync();

            if (existingPoll is null)
            {
                existingPoll = new SeasonPoll()
                {
                    Id = dtoIdentity.CanonicalId,
                    Name = dto.Name,
                    ShortName = dto.ShortName,
                    SeasonYear = command.Season ?? 0,
                    Slug = dto.Type,
                    CreatedBy = command.CorrelationId,
                    ExternalIds = new List<SeasonPollExternalId>()
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            SourceUrl = dtoIdentity.CleanUrl,
                            SourceUrlHash = dtoIdentity.UrlHash,
                            Value = dtoIdentity.CanonicalId.ToString(),
                            CreatedBy = command.CorrelationId,
                            Provider = command.SourceDataProvider,
                            SeasonPollId = dtoIdentity.CanonicalId
                        }
                    },
                    CreatedUtc = DateTime.UtcNow
                };

                await _dataContext.SeasonPolls.AddAsync(existingPoll);
                await _dataContext.SaveChangesAsync();
            }

            if (dto.Rankings is null || dto.Rankings.Count == 0)
            {
                _logger.LogWarning("No rankings in poll. {@Command}", command);
                return;
            }

            foreach (var rank in dto.Rankings)
            {
                var rankIdentity = _externalRefIdentityGenerator.Generate(rank.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: rankIdentity.UrlHash,
                    ParentId: dtoIdentity.CanonicalId.ToString(),
                    Uri: new Uri(rankIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.SeasonTypeWeekRankings,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonTypeWeekRankingsDocumentProcessor,
                    BypassCache: true
                ));
            }

            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();
        }
    }
}
