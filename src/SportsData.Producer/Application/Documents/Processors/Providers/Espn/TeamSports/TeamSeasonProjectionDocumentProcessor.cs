using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonProjection)]
public class TeamSeasonProjectionDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly TDataContext _dataContext;
    private readonly ILogger<TeamSeasonProjectionDocumentProcessor<TDataContext>> _logger;
    private readonly IPublishEndpoint _publishEndpoint;

    public TeamSeasonProjectionDocumentProcessor(
        TDataContext dataContext,
        ILogger<TeamSeasonProjectionDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Processing TeamSeasonProjectionDocument for FranchiseSeason {ParentId}", command.ParentId);
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

        var dto = command.Document.FromJson<EspnTeamSeasonProjectionDto>();
        if (dto is null)
        {
            _logger.LogError($"Error deserializing {command.DocumentType}");
            throw new InvalidOperationException($"Deserialization returned null for EspnTeamSeasonProjectionDto. CorrelationId: {command.CorrelationId}");
        }

        // Check if a projection already exists for this FranchiseSeason
        var existing = await _dataContext.FranchiseSeasonProjections
            .FirstOrDefaultAsync(x => x.FranchiseSeasonId == franchiseSeasonId);

        if (existing != null)
        {
            // Update existing entity
            existing.ChanceToWinDivision = dto.ChanceToWinDivision;
            existing.ChanceToWinConference = dto.ChanceToWinConference;
            existing.ProjectedWins = dto.ProjectedWins;
            existing.ProjectedLosses = dto.ProjectedLosses;
            existing.ModifiedBy = command.CorrelationId;
            existing.ModifiedUtc = DateTime.UtcNow;
            _logger.LogInformation("Updated existing FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonId);
        }
        else
        {
            // Create new entity
            var entity = dto.AsEntity(
                franchiseSeasonId,
                franchiseSeason.FranchiseId,
                franchiseSeason.SeasonYear,
                command.CorrelationId);
            await _dataContext.FranchiseSeasonProjections.AddAsync(entity);
            _logger.LogInformation("Created new FranchiseSeasonProjection for FranchiseSeason {FranchiseSeasonId}", franchiseSeasonId);
        }

        await _dataContext.SaveChangesAsync();
    }
}