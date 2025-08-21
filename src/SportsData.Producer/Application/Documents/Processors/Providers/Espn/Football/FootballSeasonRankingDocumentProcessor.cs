﻿using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonRanking)]
    public class FootballSeasonRankingDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FootballSeasonRankingDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IEventBus _publishEndpoint;

        public FootballSeasonRankingDocumentProcessor(
            ILogger<FootballSeasonRankingDocumentProcessor<TDataContext>> logger,
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
                _logger.LogInformation("Processing EventDocument with {@Command}", command);
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
            if (command.Season is null)
            {
                _logger.LogError("SeasonYear not on command");
                return;
            }

            var dto = command.Document.FromJson<EspnFootballSeasonRankingDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonRankingDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("EspnFootballSeasonRankingDto Ref is null or empty. {@Command}", command);
                return;
            }

            var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

            var poll = await _dataContext.SeasonPolls
                .Include(x => x.ExternalIds)
                .Where(x => x.SeasonYear == command.Season &&
                            x.ExternalIds.Any(z => z.SourceUrlHash == identity.UrlHash))
                .FirstOrDefaultAsync();

            if (poll is null)
            {
                poll = new SeasonPoll()
                {
                    CreatedBy = command.CorrelationId,
                    Name = dto.Name,
                    SeasonYear = command.Season.Value,
                    Slug = dto.Type,
                    ShortName = dto.ShortName,
                    ExternalIds = new List<SeasonPollExternalId>()
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            SourceUrl = identity.CleanUrl,
                            SourceUrlHash = identity.UrlHash,
                            Value = identity.UrlHash,
                            CreatedBy = command.CorrelationId,
                            Provider = command.SourceDataProvider
                        }
                    }
                };

                await _dataContext.SeasonPolls.AddAsync(poll);
                await _dataContext.SaveChangesAsync();
            }

            if (dto.Rankings is not null && dto.Rankings.Count > 0)
            {
                foreach (var rankingRef in dto.Rankings)
                {
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: HashProvider.GenerateHashFromUri(rankingRef.Ref),
                        ParentId: poll.Id.ToString(),
                        Uri: rankingRef.Ref,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.SeasonTypeWeekRankings,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.FootballSeasonRankingDocumentProcessor
                    ));
                }
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
