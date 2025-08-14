using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Coach)]
public class CoachDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<CoachDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public CoachDocumentProcessor(
        ILogger<CoachDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Began processing Coach with {@Command}", command);
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
        var dto = command.Document.FromJson<EspnCoachDto>();

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

        var urlHash = HashProvider.GenerateHashFromUri(dto.Ref);
        var coach = await _dataContext.Coaches
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(e => e.Value == urlHash && e.Provider == command.SourceDataProvider));

        if (coach is null)
        {
            await ProcessNewEntity(command, dto);
        }
        else
        {
            await ProcessUpdate(command, dto, coach);
        }
    }

    private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnCoachDto dto)
    {
        var newEntity = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);
        await _dataContext.Coaches.AddAsync(newEntity);
        await _dataContext.SaveChangesAsync();
        _logger.LogInformation("Created new Coach entity: {CoachId}", newEntity.Id);
    }

    private async Task ProcessUpdate(ProcessDocumentCommand command, EspnCoachDto dto, Coach coach)
    {
        var updated = false;
        if (coach.Experience != dto.Experience)
        {
            coach.Experience = dto.Experience;
            updated = true;
        }
        if (updated)
        {
            await _dataContext.SaveChangesAsync();
            _logger.LogInformation("Updated Coach entity: {CoachId}", coach.Id);
        }
        else
        {
            _logger.LogInformation("No changes detected for Coach {CoachId}", coach.Id);
        }
    }
}
