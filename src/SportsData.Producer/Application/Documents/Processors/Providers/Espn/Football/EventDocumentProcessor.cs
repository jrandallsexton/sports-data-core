using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Event)]
public class EventDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    private readonly DocumentProcessingConfig _config;

    public EventDocumentProcessor(
        ILogger<EventDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["ContestId"] = command.ParentId ?? Guid.Empty.ToString()
               }))
        {
            _logger.LogInformation("EventDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventDocumentProcessor completed.");
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
                _logger.LogError(ex, "EventDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventDto.");
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventDto Ref is null.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            throw new InvalidOperationException("SeasonYear must be defined in the command.");
        }

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var entity = await _dataContext.Contests
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                       z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            _logger.LogInformation("Processing new Contest entity. Ref={Ref}", externalDto.Ref);
            await ProcessNewEntity(command, externalDto, command.Season.Value);
        }
        else
        {
            _logger.LogInformation("Processing Contest update. ContestId={ContestId}, Ref={Ref}", entity.Id, externalDto.Ref);
            await ProcessUpdate(command, externalDto, entity);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventDto externalDto,
        int seasonYear)
    {
        _logger.LogInformation("Creating new Contest. SeasonYear={SeasonYear}", seasonYear);

        var seasonPhaseId = await GetSeasonPhaseId(command, externalDto);
        var seasonWeekId = await GetSeasonWeekId(command, externalDto);

        var contest = externalDto.AsEntity(
            _externalRefIdentityGenerator,
            command.Sport,
            seasonYear,
            seasonWeekId,
            seasonPhaseId,
            command.CorrelationId);

        // Add contest links from dto.Links
        AddLinks(externalDto, contest);

        // Get the team IDs from the external DTO
        await AddTeams(command, externalDto, contest);

        // Attempt to resolve Venue from $ref
        await AddVenue(command, externalDto, contest);

        // process competitions
        await ProcessCompetitions(command, externalDto, contest);

        await _dataContext.AddAsync(contest);

        _logger.LogInformation("Publishing ContestCreated event. ContestId={ContestId}", contest.Id);

        await _publishEndpoint.Publish(new ContestCreated(
            contest.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.EventDocumentProcessor));

        await _dataContext.SaveChangesAsync();
        
        _logger.LogInformation("Contest created successfully. ContestId={ContestId}", contest.Id);
    }

    private async Task<Guid> GetSeasonWeekId(ProcessDocumentCommand command, EspnEventDto externalDto)
    {
        var seasonWeekId = Guid.Empty;

        if (externalDto.Week is null)
        {
            _logger.LogError("Event DTO missing Week information. Requires enrichment to determine. {@ExternalDto}", externalDto);
        }
        else
        {
            var seasonWeekIdentity = _externalRefIdentityGenerator.Generate(externalDto.Week.Ref);

            var seasonWeek = await _dataContext.SeasonWeeks
                .FirstOrDefaultAsync(sw => sw.Id == seasonWeekIdentity.CanonicalId);

            if (seasonWeek is null)
            {
                if (!_config.EnableDependencyRequests)
                {
                    _logger.LogWarning(
                        "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                        DocumentType.SeasonTypeWeek,
                        nameof(EventDocumentProcessor<TDataContext>),
                        externalDto.Week.Ref);
                }
                else
                {
                    // Legacy mode: keep existing DocumentRequested logic
                    _logger.LogWarning(
                        "SeasonWeek not found. Raising DocumentRequested (override mode). WeekRef={WeekRef}",
                        externalDto.Week.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: Guid.NewGuid().ToString(),
                        ParentId: null, // TODO: could be seasonPhaseId? FML.
                        Uri: externalDto.Week.Ref.ToCleanUri(),
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.SeasonTypeWeek,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventDocumentProcessor
                    ));
                }

                throw new ExternalDocumentNotSourcedException(
                    $"SeasonWeek not found for {externalDto.SeasonType.Ref} in command {command.CorrelationId}");
            }
            else
            {
                seasonWeekId = seasonWeek.Id;
            }
        }

        return seasonWeekId;
    }

    private async Task<Guid> GetSeasonPhaseId(ProcessDocumentCommand command, EspnEventDto externalDto)
    {
        var seasonPhaseId = await _dataContext.ResolveIdAsync<
            SeasonPhase, SeasonPhaseExternalId>(
            externalDto.SeasonType,
            command.SourceDataProvider,
            () => _dataContext.SeasonPhases,
            externalIdsNav: "ExternalIds",
            key: sp => sp.Id);

        if (seasonPhaseId is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.SeasonType,
                    nameof(EventDocumentProcessor<TDataContext>),
                    externalDto.SeasonType.Ref);
                throw new ExternalDocumentNotSourcedException(
                    $"SeasonPhase not found for {externalDto.SeasonType.Ref} in command {command.CorrelationId}");
            }
            else
            {
                // Legacy mode: keep existing DocumentRequested logic
                _logger.LogWarning(
                    "SeasonPhase not found. Raising DocumentRequested (override mode). SeasonType={SeasonType}",
                    externalDto.SeasonType);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: Guid.NewGuid().ToString(),
                    ParentId: null,
                    Uri: externalDto.SeasonType.Ref.ToCleanUri(),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.SeasonType,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventDocumentProcessor
                ));

                throw new ExternalDocumentNotSourcedException(
                    $"SeasonPhase not found for {externalDto.SeasonType.Ref} in command {command.CorrelationId}");
            }
        }

        return seasonPhaseId.Value;
    }

    private async Task ProcessCompetitions(
        ProcessDocumentCommand command,
        EspnEventDto externalDto,
        Contest contest)
    {
        _logger.LogInformation("Processing {Count} competitions. ContestId={ContestId}", 
            externalDto.Competitions.Count(), 
            contest.Id);

        foreach (var competition in externalDto.Competitions)
        {
            _logger.LogDebug("Publishing DocumentRequested for EventCompetition. CompetitionRef={CompetitionRef}", 
                competition.Ref);

            var competitionIdentity = _externalRefIdentityGenerator.Generate(competition.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: competitionIdentity.UrlHash,
                ParentId: contest.Id.ToString(),
                Uri: new Uri(competitionIdentity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetition,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventDocumentProcessor
            ));
        }
    }

    private static void AddLinks(
        EspnEventDto externalDto,
        Contest contest)
    {
        if (externalDto.Links?.Any() != true)
            return;

        foreach (var link in externalDto.Links)
        {
            contest.Links.Add(new ContestLink
            {
                Id = Guid.NewGuid(),
                ContestId = contest.Id,
                Rel = string.Join("|", link.Rel),
                Href = link.Href.ToCleanUrl(),
                Text = link.Text,
                ShortText = link.ShortText,
                IsExternal = link.IsExternal,
                IsPremium = link.IsPremium,
                SourceUrlHash = HashProvider.GenerateHashFromUri(link.Href)
            });
        }
    }

    private async Task AddVenue(
        ProcessDocumentCommand command,
        EspnEventDto externalDto,
        Contest contest)
    {
        var venue = externalDto.Venues.FirstOrDefault();
        if (venue != null)
        {
            // Resolve VenueId via SourceUrlHash
            var venueId = await _dataContext.ResolveIdAsync<
                Venue, VenueExternalId>(
                venue,
                command.SourceDataProvider,
                () => _dataContext.Venues,
                externalIdsNav: "ExternalIds",
                key: v => v.Id);

            if (venueId != null)
            {
                contest.VenueId = venueId.Value;
            }
            else
            {
                _logger.LogError(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Publishing DocumentRequested. Ref={Ref}",
                    DocumentType.Venue,
                    nameof(EventDocumentProcessor<TDataContext>),
                    venue.Ref);

                // Use base class helper for Venue request
                await PublishChildDocumentRequest(
                    command,
                    venue,
                    string.Empty,
                    DocumentType.Venue,
                    CausationId.Producer.EventDocumentProcessor);

                throw new ExternalDocumentNotSourcedException(
                    $"Venue not found for {venue.Ref} in command {command.CorrelationId}");
            }
        }
    }

    private async Task AddTeams(
        ProcessDocumentCommand command,
        EspnEventDto externalDto,
        Contest contest)
    {
        /* Home Team */
        var homeTeam = externalDto
            .Competitions.First()
            .Competitors.First(x => x.HomeAway.ToLowerInvariant() == "home");

        var homeTeamFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            homeTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        if (homeTeamFranchiseSeasonId == null)
        {
            var homeFranchiseUri = EspnUriMapper.TeamSeasonToFranchiseRef(homeTeam.Team.Ref);
            var homeFranchiseIdentity = _externalRefIdentityGenerator.Generate(homeFranchiseUri);

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref} Team={Team}",
                    DocumentType.TeamSeason,
                    nameof(EventDocumentProcessor<TDataContext>),
                    homeTeam.Team.Ref,
                    "Home");
                throw new ExternalDocumentNotSourcedException(
                    $"Home team franchise season not found for {homeTeam.Ref} in command {command.CorrelationId}");
            }
            else
            {
                // Legacy mode: keep existing DocumentRequested logic
                _logger.LogWarning(
                    "Home FranchiseSeason not found. Raising DocumentRequested (override mode). TeamRef={TeamRef}",
                    homeTeam.Team.Ref);
                
                await _publishEndpoint.Publish(new DocumentRequested(
                    homeTeam.Team.Ref.ToCleanUrl(),
                    homeFranchiseIdentity.CanonicalId.ToString(),
                    homeTeam.Team.Ref,
                    command.Sport,
                    command.Season,
                    DocumentType.TeamSeason,
                    command.SourceDataProvider,
                    command.CorrelationId,
                    CausationId.Producer.EventDocumentProcessor));
                
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Home team franchise season not found for {homeTeam.Ref} in command {command.CorrelationId}");
            }
        }
        contest.HomeTeamFranchiseSeasonId = homeTeamFranchiseSeasonId.Value;

        /* Away Team */
        var awayTeam = externalDto
            .Competitions.First()
            .Competitors.First(x => x.HomeAway.ToLowerInvariant() == "away");

        var awayTeamFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            awayTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        if (awayTeamFranchiseSeasonId == null)
        {
            var awayFranchiseUri = EspnUriMapper.TeamSeasonToFranchiseRef(awayTeam.Team.Ref);
            var awayFranchiseIdentity = _externalRefIdentityGenerator.Generate(awayFranchiseUri);

            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref} Team={Team}",
                    DocumentType.TeamSeason,
                    nameof(EventDocumentProcessor<TDataContext>),
                    awayTeam.Team.Ref,
                    "Away");
                throw new ExternalDocumentNotSourcedException(
                    $"Away team franchise season not found for {awayTeam.Ref} in command {command.CorrelationId}");
            }
            else
            {
                // Legacy mode: keep existing DocumentRequested logic
                _logger.LogWarning(
                    "Away FranchiseSeason not found. Raising DocumentRequested (override mode). TeamRef={TeamRef}",
                    awayTeam.Team.Ref);
                
                await _publishEndpoint.Publish(new DocumentRequested(
                    awayTeam.Team.Ref.ToCleanUrl(),
                    awayFranchiseIdentity.CanonicalId.ToString(),
                    awayTeam.Team.Ref.ToCleanUri(),
                    command.Sport,
                    command.Season,
                    DocumentType.TeamSeason,
                    command.SourceDataProvider,
                    command.CorrelationId,
                    CausationId.Producer.EventDocumentProcessor));

                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Away team franchise season not found for {awayTeam.Ref} in command {command.CorrelationId}");
            }
        }
        contest.AwayTeamFranchiseSeasonId = awayTeamFranchiseSeasonId.Value;

        if (string.IsNullOrEmpty(contest.ShortName))
        {
            var awayFranchise = await _dataContext.FranchiseSeasons
                .Include(s => s.Franchise)
                .Where(x => x.Id == awayTeamFranchiseSeasonId)
                .FirstOrDefaultAsync();

            var homeFranchise = await _dataContext.FranchiseSeasons
                .Include(s => s.Franchise)
                .Where(x => x.Id == homeTeamFranchiseSeasonId)
                .FirstOrDefaultAsync();

            if (awayFranchise != null && homeFranchise != null)
            {
                contest.ShortName = $"{homeFranchise.Franchise.Abbreviation ?? homeFranchise.Franchise.Name} @ {awayFranchise.Franchise.Abbreviation ?? awayFranchise.Franchise.Name}";
            }
        }
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventDto dto,
        Contest contest)
    {
        _logger.LogInformation("Updating Contest. ContestId={ContestId}", contest.Id);

        if (DateTime.TryParse(dto.Date, out var startDateTime))
        {
            _logger.LogInformation(
                "Updating Contest StartDateUtc. ContestId={ContestId}, OldDate={OldDate}, NewDate={NewDate}",
                contest.Id,
                contest.StartDateUtc,
                startDateTime.ToUniversalTime());

            contest.StartDateUtc = DateTime.Parse(dto.Date).ToUniversalTime();
            await _dataContext.SaveChangesAsync();
        }

        // I'm not sure if there is anything to update here,
        // but we will request sourcing of competitions again
        _logger.LogInformation(
            "Re-sourcing {Count} competitions for updated Contest. ContestId={ContestId}",
            dto.Competitions.Count(),
            contest.Id);

        foreach (var competition in dto.Competitions)
        {
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competition.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: competitionIdentity.UrlHash,
                ParentId: contest.Id.ToString(),
                Uri: new Uri(competitionIdentity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetition,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventDocumentProcessor
            ));
            
            await _dataContext.SaveChangesAsync();
        }
        
        _logger.LogInformation("Contest update completed. ContestId={ContestId}", contest.Id);
    }
}