using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Processes a season-specific award document from ESPN.
/// Example: http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/3
/// Creates or updates Award (normalized definition), FranchiseSeasonAward (season instance), and FranchiseSeasonAwardWinner entities.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonAward)]
public class TeamSeasonAwardDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonAwardDocumentProcessor(
        ILogger<TeamSeasonAwardDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnAwardDto>();
        if (dto is null || dto.Ref is null)
        {
            _logger.LogError("Invalid or null EspnAwardDto.");
            return;
        }

        var franchiseSeasonId = TryGetOrDeriveParentId(
            command, 
            EspnUriMapper.TeamSeasonAwardRefToTeamSeasonRef);

        if (franchiseSeasonId == null)
        {
            _logger.LogError("Unable to determine FranchiseSeasonId from ParentId or URI");
            return;
        }

        var franchiseSeasonIdValue = franchiseSeasonId.Value;

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == franchiseSeasonIdValue);

        if (franchiseSeason is null)
        {
            throw new ExternalDocumentNotSourcedException(
                $"FranchiseSeason {franchiseSeasonIdValue} not found. Will retry when available.");
        }

        // Convert season-specific award URL to canonical award URL
        // From: http://.../ seasons/2019/awards/3
        // To:   http://.../ awards/3
        var canonicalAwardRef = EspnUriMapper.SeasonAwardToAwardRef(dto.Ref);
        var awardIdentity = _externalRefIdentityGenerator.Generate(canonicalAwardRef);

        _logger.LogInformation("Processing award. CanonicalRef={CanonicalRef}, AwardId={AwardId}",
            canonicalAwardRef, awardIdentity.CanonicalId);

        // 1. Create or update Award entity (normalized definition)
        await ProcessAward(dto, awardIdentity, command);

        // 2. Create or update FranchiseSeasonAward entity (season instance)
        var franchiseSeasonAward = await ProcessFranchiseSeasonAward(
            dto, 
            franchiseSeasonIdValue, 
            awardIdentity.CanonicalId, 
            command);

        // 3. Replace FranchiseSeasonAwardWinner entities (delete existing, create new)
        await ProcessAwardWinners(dto, franchiseSeasonAward.Id, command);

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessAward(EspnAwardDto dto, ExternalRefIdentity awardIdentity, ProcessDocumentCommand command)
    {
        var existingAward = await _dataContext.Awards
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == awardIdentity.CanonicalId);

        if (existingAward != null)
        {
            // Update existing award metadata
            var hasChanges = false;

            if (existingAward.Name != dto.Name)
            {
                _logger.LogInformation("Updating Award Name. Old={OldName}, New={NewName}",
                    existingAward.Name, dto.Name);
                existingAward.Name = dto.Name;
                hasChanges = true;
            }

            if (existingAward.Description != dto.Description)
            {
                _logger.LogInformation("Updating Award Description.");
                existingAward.Description = dto.Description;
                hasChanges = true;
            }

            if (existingAward.History != dto.History)
            {
                _logger.LogInformation("Updating Award History.");
                existingAward.History = dto.History;
                hasChanges = true;
            }

            // Ensure ESPN external ID exists
            var espnExternalId = existingAward.ExternalIds.FirstOrDefault(x =>
                x.Provider == SourceDataProvider.Espn &&
                (x.Value == awardIdentity.UrlHash || x.SourceUrlHash == awardIdentity.UrlHash || x.SourceUrl == awardIdentity.CleanUrl));

            if (espnExternalId == null)
            {
                _logger.LogInformation("Adding ESPN external ID to existing Award. AwardId={AwardId}", existingAward.Id);
                existingAward.ExternalIds.Add(new AwardExternalId
                {
                    Id = Guid.NewGuid(),
                    AwardId = existingAward.Id,
                    Value = awardIdentity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = awardIdentity.CleanUrl,
                    SourceUrlHash = awardIdentity.UrlHash,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = command.CorrelationId,
                    ModifiedUtc = DateTime.UtcNow,
                    ModifiedBy = command.CorrelationId
                });
                hasChanges = true;
            }

            if (hasChanges)
            {
                existingAward.ModifiedUtc = DateTime.UtcNow;
                existingAward.ModifiedBy = command.CorrelationId;
            }
        }
        else
        {
            // Create new award using extension method
            _logger.LogInformation("Creating new Award. Id={AwardId}, Name={Name}",
                awardIdentity.CanonicalId, dto.Name);

            var newAward = dto.AsEntity(awardIdentity, command.CorrelationId);

            await _dataContext.Awards.AddAsync(newAward);
        }
    }

    private async Task<FranchiseSeasonAward> ProcessFranchiseSeasonAward(
        EspnAwardDto dto, 
        Guid franchiseSeasonId, 
        Guid awardId, 
        ProcessDocumentCommand command)
    {
        var franchiseSeasonAwardIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var existingFranchiseSeasonAward = await _dataContext.FranchiseSeasonAwards
            .FirstOrDefaultAsync(x => x.Id == franchiseSeasonAwardIdentity.CanonicalId);

        if (existingFranchiseSeasonAward != null)
        {
            _logger.LogInformation("FranchiseSeasonAward already exists. Id={FranchiseSeasonAwardId}",
                existingFranchiseSeasonAward.Id);

            existingFranchiseSeasonAward.ModifiedUtc = DateTime.UtcNow;
            existingFranchiseSeasonAward.ModifiedBy = command.CorrelationId;

            return existingFranchiseSeasonAward;
        }

        // Create new franchise season award
        _logger.LogInformation("Creating new FranchiseSeasonAward. Id={FranchiseSeasonAwardId}",
            franchiseSeasonAwardIdentity.CanonicalId);

        var newFranchiseSeasonAward = new FranchiseSeasonAward
        {
            Id = franchiseSeasonAwardIdentity.CanonicalId,
            FranchiseSeasonId = franchiseSeasonId,
            AwardId = awardId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = command.CorrelationId,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = command.CorrelationId
        };

        await _dataContext.FranchiseSeasonAwards.AddAsync(newFranchiseSeasonAward);

        return newFranchiseSeasonAward;
    }

    private async Task ProcessAwardWinners(EspnAwardDto dto, Guid franchiseSeasonAwardId, ProcessDocumentCommand command)
    {
        // Remove existing winners
        var existingWinners = await _dataContext.FranchiseSeasonAwardWinners
            .Where(x => x.FranchiseSeasonAwardId == franchiseSeasonAwardId)
            .ToListAsync();

        if (existingWinners.Any())
        {
            _logger.LogInformation("Removing {Count} existing award winners for replacement.",
                existingWinners.Count);
            _dataContext.FranchiseSeasonAwardWinners.RemoveRange(existingWinners);
        }

        // Add new winners from DTO
        if (dto.Winners?.Any() == true)
        {
            _logger.LogInformation("Adding {Count} award winners.", dto.Winners.Count);

            foreach (var winner in dto.Winners)
            {
                var newWinner = new FranchiseSeasonAwardWinner
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonAwardId = franchiseSeasonAwardId,
                    AthleteRef = winner.Athlete?.Ref?.ToString(),
                    TeamRef = winner.Team?.Ref?.ToString(),
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = command.CorrelationId,
                    ModifiedUtc = DateTime.UtcNow,
                    ModifiedBy = command.CorrelationId
                };

                await _dataContext.FranchiseSeasonAwardWinners.AddAsync(newWinner);
            }
        }
        else
        {
            _logger.LogWarning("No winners found in award DTO.");
        }
    }
}