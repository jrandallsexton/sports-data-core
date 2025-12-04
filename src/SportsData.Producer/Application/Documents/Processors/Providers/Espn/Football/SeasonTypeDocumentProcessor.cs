using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonType)]
public class SeasonTypeDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : BaseDataContext
{
    private readonly ILogger<SeasonTypeDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IEventBus _publishEndpoint;

    public SeasonTypeDocumentProcessor(
        ILogger<SeasonTypeDocumentProcessor<TDataContext>> logger,
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

        // Source Groups
        if (dto.Groups?.Ref is not null)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: seasonPhase.Id.ToString(),
                Uri: dto.Groups.Ref,
                Sport: Sport.FootballNcaa,
                SeasonYear: dto.Year,
                DocumentType: DocumentType.GroupSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.SeasonTypeDocumentProcessor
            ));
        }

        // Source Weeks
        if (dto.Weeks?.Ref is not null)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: seasonPhase.Id.ToString(),
                Uri: dto.Weeks.Ref,
                Sport: Sport.FootballNcaa,
                SeasonYear: dto.Year,
                DocumentType: DocumentType.SeasonTypeWeek,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.SeasonTypeDocumentProcessor
            ));
        }

        await _dataContext.SaveChangesAsync();
    }
}