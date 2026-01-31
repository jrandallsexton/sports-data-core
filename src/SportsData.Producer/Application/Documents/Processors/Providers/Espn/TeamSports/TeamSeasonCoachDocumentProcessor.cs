using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Processes a single team season coach document.
/// Example: http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/2331669
/// Creates or updates CoachSeason entity linking a coach to a franchise season.
/// Spawns child document for full coach data processing.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonCoach)]
public class TeamSeasonCoachDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonCoachDocumentProcessor(
        ILogger<TeamSeasonCoachDocumentProcessor<TDataContext>> logger,
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
            ["CorrelationId"] = command.CorrelationId,
            ["DocumentType"] = command.DocumentType,
            ["Season"] = command.Season ?? 0,
            ["FranchiseSeasonId"] = command.ParentId ?? "Unknown"
        }))
        {
            _logger.LogInformation("TeamSeasonCoachDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}",
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);

                _logger.LogInformation("TeamSeasonCoachDocumentProcessor completed.");
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready, will retry later.");

                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamSeasonCoachDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnCoachSeasonDto>();
        if (dto is null || dto.Ref is null)
        {
            _logger.LogError("Invalid or null EspnCoachSeasonDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid or missing ParentId. ParentId={ParentId}", command.ParentId);
            return;
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            _logger.LogError("FranchiseSeason not found. FranchiseSeasonId={FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        // Preflight dependency check: Person document must exist
        if (dto.Person?.Ref != null)
        {
            var personIdentity = _externalRefIdentityGenerator.Generate(dto.Person.Ref);
            var personExists = await _dataContext.Coaches
                .AnyAsync(x => x.Id == personIdentity.CanonicalId);

            if (!personExists)
            {
                _logger.LogWarning("Person document not found for Coach. Requesting source and retrying. PersonRef={PersonRef}",
                    dto.Person.Ref);

                // Request Person document sourcing
                await PublishChildDocumentRequest<Guid?>(
                    command,
                    dto.Person,
                    null,
                    DocumentType.Coach,
                    CausationId.Producer.TeamSeasonCoachDocumentProcessor);

                throw new ExternalDocumentNotSourcedException(
                    $"Person document not sourced yet for ref: {dto.Person.Ref}");
            }
        }

        // Generate canonical ID for this coach season
        var coachSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        // Check if CoachSeason already exists
        var existingCoachSeason = await _dataContext.CoachSeasons
            .FirstOrDefaultAsync(x => x.Id == coachSeasonIdentity.CanonicalId);

        if (existingCoachSeason != null)
        {
            _logger.LogInformation("CoachSeason already exists with Id={CoachSeasonId}, setting IsActive=true",
                existingCoachSeason.Id);

            existingCoachSeason.IsActive = true;
            existingCoachSeason.ModifiedUtc = DateTime.UtcNow;
            existingCoachSeason.ModifiedBy = command.CorrelationId;
        }
        else
        {
            _logger.LogInformation("Creating new CoachSeason with Id={CoachSeasonId}",
                coachSeasonIdentity.CanonicalId);

            // CoachSeason will be created by the child CoachSeason processor
            // We just spawn the child document here
        }

        // Spawn child document for full coach season processing
        await PublishChildDocumentRequest<Guid?>(
            command,
            dto,
            franchiseSeasonId,
            DocumentType.CoachSeason,
            CausationId.Producer.TeamSeasonCoachDocumentProcessor);

        await _dataContext.SaveChangesAsync();
    }
}