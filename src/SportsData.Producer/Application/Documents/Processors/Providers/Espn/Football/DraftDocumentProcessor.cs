using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common.Draft;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.Draft)]
public class DraftDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public DraftDocumentProcessor(
        ILogger<DraftDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnDraftDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnDraftDto. {@Command}", command);
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("SeasonYear is required for {DocumentType}", command.DocumentType);
            throw new InvalidOperationException(
                $"SeasonYear was not provided. CorrelationId: {command.CorrelationId}");
        }

        var draftYear = command.SeasonYear.Value;

        // Generate a deterministic ID from the draft URI
        var draftId = _externalRefIdentityGenerator.Generate(dto.Ref).CanonicalId;

        var existing = await _dataContext.Drafts
            .FirstOrDefaultAsync(d => d.Id == draftId);

        if (existing is not null)
        {
            _logger.LogInformation(
                "Updating existing Draft entity for year {DraftYear}", draftYear);
            existing.Year = dto.Year;
            existing.NumberOfRounds = dto.NumberOfRounds;
            existing.DisplayName = dto.DisplayName;
            existing.ShortDisplayName = dto.ShortDisplayName;
            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
        }
        else
        {
            var draft = new Draft
            {
                Id = draftId,
                Year = dto.Year,
                NumberOfRounds = dto.NumberOfRounds,
                DisplayName = dto.DisplayName,
                ShortDisplayName = dto.ShortDisplayName,
                CreatedBy = command.CorrelationId,
                CreatedUtc = DateTime.UtcNow
            };

            await _dataContext.Drafts.AddAsync(draft);

            _logger.LogInformation(
                "Created Draft entity for year {DraftYear} with Id {DraftId}",
                draftYear, draftId);
        }

        // Spawn child document request for the rounds endpoint
        if (ShouldSpawn(DocumentType.DraftRounds, command))
        {
            await PublishChildDocumentRequest(command, dto.Rounds, draftId, DocumentType.DraftRounds);
        }

        await _dataContext.SaveChangesAsync();
    }
}
