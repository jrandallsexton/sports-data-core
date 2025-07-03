using MassTransit;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecordAts)]
public class TeamSeasonRecordAtsDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly TDataContext _dataContext;
    private readonly ILogger<TeamSeasonRecordAtsDocumentProcessor<TDataContext>> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public TeamSeasonRecordAtsDocumentProcessor(
        TDataContext dataContext,
        ILogger<TeamSeasonRecordAtsDocumentProcessor<TDataContext>> logger,
        IPublishEndpoint publishEndpoint)
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
            _logger.LogInformation("Processing TeamSeasonRecordAtsDocument for FranchiseSeason {ParentId}", command.ParentId);
            await ProcessInternal(command);
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

        // TODO: Implement deserialization and processing logic for TeamSeasonRecordAts
        _logger.LogInformation("TODO: Implement TeamSeasonRecordAtsDocument processing");
        await Task.Delay(100);
    }
}