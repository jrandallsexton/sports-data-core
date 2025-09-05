﻿using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRank)]
public class TeamSeasonRankDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly TDataContext _dataContext;
    private readonly ILogger<TeamSeasonRankDocumentProcessor<TDataContext>> _logger;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public TeamSeasonRankDocumentProcessor(
        TDataContext dataContext,
        ILogger<TeamSeasonRankDocumentProcessor<TDataContext>> logger,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _dataContext = dataContext;
        _logger = logger;
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

        // TODO: CompetitionBroadcast domain event

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Successfully processed TeamSeasonRank for FranchiseSeason {Id}", franchiseSeasonId);
    }
}
