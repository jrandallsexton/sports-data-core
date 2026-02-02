using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorRoster)]
public class EventCompetitionCompetitorRosterDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionCompetitorRosterDocumentProcessor(
        ILogger<EventCompetitionCompetitorRosterDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitorId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionCompetitorRosterDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}",
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);

                _logger.LogInformation("EventCompetitionCompetitorRosterDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionCompetitorRosterDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorRosterDto.");
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionCompetitorRosterDto Ref is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(command.ParentId))
        {
            _logger.LogError("Command missing ParentId for CompetitorId.");
            return;
        }

        if (dto.Competition?.Ref is null)
        {
            _logger.LogError("Competition ref is missing from roster DTO.");
            return;
        }

        // Resolve CompetitionId from the Competition ref
        var competitionIdentity = _externalRefIdentityGenerator.Generate(dto.Competition.Ref);
        var competitionId = competitionIdentity.CanonicalId;
        
        if (!Guid.TryParse(command.ParentId, out var competitorId))
        {
            _logger.LogError("Invalid CompetitionCompetitorId in ParentId: {ParentId}. Skipping roster processing.", command.ParentId);
            return;
        }

        _logger.LogInformation("Processing roster with {EntryCount} entries. CompetitorId={CompetitorId}, CompetitionId={CompetitionId}",
            dto.Entries.Count,
            competitorId,
            competitionId);

        // Wholesale replacement: Delete existing roster entries for this competition competitor (team)
        var existingRosterEntries = await _dataContext.AthleteCompetitions
            .Where(x => x.CompetitionId == competitionId && x.CompetitionCompetitorId == competitorId)
            .ToListAsync();

        if (existingRosterEntries.Any())
        {
            _logger.LogDebug("Removing {Count} existing roster entries for wholesale replacement.", existingRosterEntries.Count);
            _dataContext.AthleteCompetitions.RemoveRange(existingRosterEntries);
        }

        // Pre-compute all IDs and query existence once to avoid N+1 queries
        var athleteSeasonIds = new List<Guid>();
        var positionIds = new List<Guid>();
        var entryLookup = new Dictionary<Guid, (EspnRosterEntryDto Entry, Guid? PositionId)>();

        foreach (var entry in dto.Entries)
        {
            if (entry.Athlete?.Ref is null)
            {
                _logger.LogWarning("Roster entry for PlayerId={PlayerId} ({DisplayName}) has no athlete ref, skipping.",
                    entry.PlayerId,
                    entry.DisplayName);
                continue;
            }

            var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(entry.Athlete.Ref);
            var athleteSeasonId = athleteSeasonIdentity.CanonicalId;
            athleteSeasonIds.Add(athleteSeasonId);

            Guid? positionId = null;
            if (entry.Position?.Ref is not null)
            {
                var positionIdentity = _externalRefIdentityGenerator.Generate(entry.Position.Ref);
                positionId = positionIdentity.CanonicalId;
                positionIds.Add(positionId.Value);
            }

            entryLookup[athleteSeasonId] = (entry, positionId);
        }

        // Query existing IDs once
        var existingAthleteSeasonIds = await _dataContext.AthleteSeasons
            .Where(x => athleteSeasonIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToHashSetAsync();

        var existingPositionIds = positionIds.Any()
            ? await _dataContext.AthletePositions
                .Where(x => positionIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToHashSetAsync()
            : new HashSet<Guid>();

        // Process each roster entry
        var newRosterEntries = new List<AthleteCompetition>();
        var publishedStatsCount = 0;

        foreach (var (athleteSeasonId, (entry, positionId)) in entryLookup)
        {
            // Check if AthleteSeason exists
            if (!existingAthleteSeasonIds.Contains(athleteSeasonId))
            {
                _logger.LogDebug("AthleteSeason not found for athlete {DisplayName} (Id: {AthleteSeasonId}). This athlete may not yet be sourced.",
                    entry.DisplayName,
                    athleteSeasonId);
                // Don't fail - just skip this athlete (they may be sourced later)
                continue;
            }

            // Verify position exists (optional - won't fail if missing)
            var finalPositionId = positionId.HasValue && existingPositionIds.Contains(positionId.Value)
                ? positionId
                : null;

            if (positionId.HasValue && !finalPositionId.HasValue)
            {
                _logger.LogDebug("Position not found for athlete {DisplayName}, position will be null.",
                    entry.DisplayName);
            }

            // Create AthleteCompetition entity
            var athleteCompetition = new AthleteCompetition
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                CompetitionCompetitorId = competitorId,
                AthleteSeasonId = athleteSeasonId,
                PositionId = finalPositionId,
                JerseyNumber = entry.Jersey,
                DidNotPlay = entry.DidNotPlay,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = command.CorrelationId
            };

            newRosterEntries.Add(athleteCompetition);

            _logger.LogDebug("Created roster entry for {DisplayName} (Jersey: {Jersey}, DidNotPlay: {DidNotPlay})",
                entry.DisplayName,
                entry.Jersey,
                entry.DidNotPlay);

            // Publish child document requests for athlete statistics (if available)
            if (entry.Statistics?.Ref is not null)
            {
                _logger.LogDebug("Publishing child request for athlete statistics. Athlete={DisplayName}, StatRef={StatRef}",
                    entry.DisplayName,
                    entry.Statistics.Ref);

                await PublishChildDocumentRequest<string?>(
                    command,
                    entry.Statistics,
                    null, // Stats document is self-contained with athlete and competition refs
                    DocumentType.EventCompetitionAthleteStatistics,
                    CausationId.Producer.EventCompetitionCompetitorRosterDocumentProcessor);
                
                publishedStatsCount++;
            }
        }

        // Add all new roster entries
        if (newRosterEntries.Any())
        {
            await _dataContext.AthleteCompetitions.AddRangeAsync(newRosterEntries);
            _logger.LogInformation("Added {Count} new roster entries for competition {CompetitionId}.",
                newRosterEntries.Count,
                competitionId);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Completed processing roster. RosterEntries={RosterCount}, PublishedStatisticsRequests={StatsCount}, CompetitorId={CompetitorId}",
            newRosterEntries.Count,
            publishedStatsCount,
            competitorId);
    }
}
