using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonType)]
public class SeasonTypeDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : BaseDataContext
{
    public SeasonTypeDocumentProcessor(
        ILogger<SeasonTypeDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IEventBus publishEndpoint)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        // deserialize the DTO
        var dto = command.Document.FromJson<EspnFootballSeasonTypeDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnFootballSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballSeasonDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var seasonId))
        {
            // let's try to manually resolve the season by year
            var seasonRef = EspnUriMapper.SeasonTypeToSeason(dto.Ref);
            var seasonIdentity = _externalRefIdentityGenerator.Generate(seasonRef);

            var season = await _dataContext.Seasons
                .Where(x => x.Id == seasonIdentity.CanonicalId)
                .FirstOrDefaultAsync();

            if (season is null)
            {
                _logger.LogError("Invalid ParentId: {ParentId} {DtoRef}", command.ParentId, dto.Ref);
                return;
            }

            seasonId = season.Id;
        }

        var existingSeasonType = await _dataContext.Seasons
            .Include(x => x.Phases)
            .Where(x => x.Id == seasonId)
            .FirstOrDefaultAsync();

        if (existingSeasonType == null)
        {
            _logger.LogError("Parent Season could not be found");
            throw new Exception("Parent Season could not be found");
        }

        var seasonPhase = dto.AsEntity(seasonId, _externalRefIdentityGenerator, command.CorrelationId);

        // does the phase already exist on the season?
        var existingPhase = existingSeasonType.Phases.FirstOrDefault(x => x.Id == seasonPhase.Id);

        if (existingPhase != null)
        {
            // update
            existingPhase.Name = seasonPhase.Name;
            existingPhase.Abbreviation = seasonPhase.Abbreviation;
            existingPhase.StartDate = seasonPhase.StartDate;
            existingPhase.EndDate = seasonPhase.EndDate;
            existingPhase.ModifiedUtc = DateTime.UtcNow;
            existingPhase.ModifiedBy = command.CorrelationId;
            _dataContext.Update(existingPhase);
        }
        else
        {
            // new
            existingSeasonType.Phases.Add(seasonPhase);
        }

        // Source Groups using base class helper
        await PublishChildDocumentRequest(
            command,
            dto.Groups,
            seasonPhase.Id,
            DocumentType.GroupSeason);

        // Source Weeks using base class helper
        await PublishChildDocumentRequest(
            command,
            dto.Weeks,
            seasonPhase.Id,
            DocumentType.SeasonTypeWeek);

        await _dataContext.SaveChangesAsync();
    }
}