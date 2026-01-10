using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonRanking)]
public class FootballSeasonRankingDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public FootballSeasonRankingDocumentProcessor(
        ILogger<FootballSeasonRankingDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
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

        // Does the parent poll exist?
        var pollIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var poll = await _dataContext.SeasonPolls
            .Include(x => x.ExternalIds)
            .Where(x => x.SeasonYear == command.Season &&
                        x.ExternalIds.Any(z => z.Value == pollIdentity.CanonicalId.ToString()))
            .FirstOrDefaultAsync();

        if (poll is null)
        {
            poll = new SeasonPoll()
            {
                Id = pollIdentity.CanonicalId,
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
                        SourceUrl = pollIdentity.CleanUrl,
                        SourceUrlHash = pollIdentity.UrlHash,
                        Value = pollIdentity.CanonicalId.ToString(),
                        CreatedBy = command.CorrelationId,
                        Provider = command.SourceDataProvider
                    }
                }
            };

            await _dataContext.SeasonPolls.AddAsync(poll);
            await _dataContext.SaveChangesAsync();
        }

        // Use base class helper for consistency
        if (dto.Rankings is not null && dto.Rankings.Count > 0)
        {
            foreach (var rankingRef in dto.Rankings)
            {
                await PublishChildDocumentRequest(
                    command,
                    rankingRef,
                    poll.Id,
                    DocumentType.SeasonTypeWeekRankings,
                    CausationId.Producer.FootballSeasonRankingDocumentProcessor);
            }
        }

        await _dataContext.SaveChangesAsync();
    }
}