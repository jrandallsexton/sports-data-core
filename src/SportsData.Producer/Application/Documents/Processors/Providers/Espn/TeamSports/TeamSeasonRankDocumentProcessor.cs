using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRank)]
public class TeamSeasonRankDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonRankDocumentProcessor(
        ILogger<TeamSeasonRankDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Processing TeamSeasonRankDocument for FranchiseSeason {ParentId}", command.ParentId);
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
        var dto = command.Document.FromJson<EspnTeamSeasonRankDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnTeamSeasonRankDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnTeamSeasonRankDto Ref is null or empty. {@Command}", command);
            return;
        }

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

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchiseSeason.FranchiseId,
            franchiseSeason.Id,
            franchiseSeason.SeasonYear,
            command.CorrelationId);

        var dtoIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var existing = await _dataContext.FranchiseSeasonRankings
            .AsNoTracking()
            .Where(r => r.Id == dtoIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            // Polls do not change once published.  Skip it.
            _logger.LogWarning("Previously processed. {@Command}", command);
            return;
        }

        await _dataContext.FranchiseSeasonRankings.AddAsync(entity);

        // Note: CompetitionBroadcast domain event is a placeholder for future real-time updates.
        // Currently polls are batch-processed weekly. See CompetitionBroadcastProcessor for planned implementation.

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed TeamSeasonRank for FranchiseSeason {Id}", franchiseSeasonId);
    }
}
