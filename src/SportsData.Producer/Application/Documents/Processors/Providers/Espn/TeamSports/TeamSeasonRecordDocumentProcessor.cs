using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecord)]
public class TeamSeasonRecordDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly TDataContext _dataContext;
    private readonly ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> _logger;
    private readonly IEventBus _publishEndpoint;

    public TeamSeasonRecordDocumentProcessor(
        TDataContext dataContext,
        ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> logger,
        IEventBus publishEndpoint)
    {
        _dataContext = dataContext;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Processing TeamSeasonRecordDocument for FranchiseSeason {ParentId}", command.ParentId);
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
        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
            return;
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            _logger.LogWarning("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        var dto = command.Document.FromJson<EspnTeamSeasonRecordDto>();
        if (dto?.Items is null || dto.Items.Count == 0)
        {
            _logger.LogWarning("No TeamSeasonRecord items found in document for FranchiseSeason {Id}", franchiseSeasonId);
            return;
        }

        if (!dto.Items.Any())
        {
            _logger.LogInformation("No items to process for FranchiseSeason {Id}", franchiseSeasonId);
            return;
        }

        foreach (var item in dto.Items)
        {
            var entity = item.AsEntity(
                franchiseSeasonId,
                franchiseSeason.FranchiseId,
                franchiseSeason.SeasonYear,
                Guid.Empty);

            await _dataContext.FranchiseSeasonRecords.AddAsync(entity);

            var canonical = entity.AsCanonical();
            await _publishEndpoint.Publish(new FranchiseSeasonRecordCreated(
                canonical,
                command.CorrelationId,
                CausationId.Producer.TeamSeasonRecordDocumentProcessor));
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed {Count} TeamSeasonRecord items for FranchiseSeason {Id}", dto.Items.Count, franchiseSeasonId);
    }
}
