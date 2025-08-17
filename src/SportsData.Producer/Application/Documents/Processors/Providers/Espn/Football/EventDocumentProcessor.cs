using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Event)]
    public class EventDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventDocumentProcessor(
            ILogger<EventDocumentProcessor<TDataContext>> logger,
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
                _logger.LogInformation("Processing EventDocument with {@Command}", command);
                try
                {
                    await ProcessInternal(command);
                }
                catch (ExternalDocumentNotSourcedException retryEx)
                {
                    _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                    var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                    await _publishEndpoint.Publish(docCreated);
                    await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                    await _dataContext.SaveChangesAsync();
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
            var externalDto = command.Document.FromJson<EspnEventDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventDto Ref is null for event. {@Command}", command);
                return;
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError("Command must have a SeasonYear defined");
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
                await ProcessNewEntity(command, externalDto, command.Season.Value);
            }
            else
            {
                await ProcessUpdate(command, externalDto, entity);
            }
        }

        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            EspnEventDto externalDto,
            int seasonYear)
        {
            var seasonPhaseId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.SeasonType,
                command.SourceDataProvider,
                () => _dataContext.SeasonPhases.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (seasonPhaseId is null)
            {
                // request sourcing?
                _logger.LogWarning("SeasonPhase not found for SeasonType {SeasonType}.", externalDto.SeasonType);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: Guid.NewGuid().ToString(),
                    ParentId: null,
                    Uri: externalDto.SeasonType.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.SeasonType,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"SeasonPhase not found for {externalDto.SeasonType.Ref} in command {command.CorrelationId}");
            }

            Guid? seasonWeekId = null;

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
                    // request sourcing
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: Guid.NewGuid().ToString(),
                        ParentId: null, // TODO: could be seasonPhaseId? FML.
                        Uri: externalDto.Week.Ref,
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.SeasonTypeWeek,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventDocumentProcessor
                    ));
                    await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                    await _dataContext.SaveChangesAsync();

                    throw new ExternalDocumentNotSourcedException(
                        $"SeasonWeek not found for {externalDto.SeasonType.Ref} in command {command.CorrelationId}");
                }
                else
                {
                    seasonWeekId = seasonWeek.Id;
                }
            }

            var contest = externalDto.AsEntity(
                _externalRefIdentityGenerator,
                command.Sport,
                seasonYear,
                seasonWeekId,
                seasonPhaseId.Value,
                command.CorrelationId);

            // Add contest links from dto.Links
            AddLinks(externalDto, contest);

            // Get the team IDs from the external DTO
            await AddTeams(command, externalDto, contest);

            // Attempt to resolve Venue from $ref
            await AddVenue(command, externalDto, contest);

            // process competitions
            await ProcessCompetition(command, externalDto, contest);

            await _dataContext.AddAsync(contest);

            await _publishEndpoint.Publish(new ContestCreated(
                contest.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));

            await _dataContext.SaveChangesAsync();
        }

        private async Task ProcessCompetition(
            ProcessDocumentCommand command,
            EspnEventDto externalDto,
            Contest contest)
        {
            foreach (var competition in externalDto.Competitions)
            {
                // raise an event to source the competition
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: HashProvider.GenerateHashFromUri(competition.Ref),
                    ParentId: contest.Id.ToString(),
                    Uri: competition.Ref,
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
                var venueId = await _dataContext.TryResolveFromDtoRefAsync(
                    venue,
                    command.SourceDataProvider,
                    () => _dataContext.Venues.Include(x => x.ExternalIds).AsNoTracking(),
                    _logger);

                if (venueId != null)
                {
                    contest.VenueId = venueId.Value;
                }
                else
                {
                    var venueHash = HashProvider.GenerateHashFromUri(venue.Ref);
                    _logger.LogWarning("Venue not found for hash {VenueHash}, publishing sourcing request.", venueHash);
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: venueHash,
                        ParentId: string.Empty,
                        Uri: venue.Ref,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.Venue,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventDocumentProcessor
                    ));
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

            var homeTeamFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                homeTeam.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (homeTeamFranchiseSeasonId == null)
            {
                var homeFranchiseUri = EspnUriMapper.TeamSeasonToFranchiseRef(homeTeam.Team.Ref);
                var homeFranchiseIdentity = _externalRefIdentityGenerator.Generate(homeFranchiseUri);

                // request sourcing
                await _publishEndpoint.Publish(new DocumentRequested(
                    homeTeam.Team.Ref.ToCleanUrl(),
                    homeFranchiseIdentity.CanonicalId.ToString(),
                    homeTeam.Team.Ref,
                    command.Sport,
                    command.Season,
                    DocumentType.TeamSeason,
                    command.SourceDataProvider,
                    command.CorrelationId,
                    CausationId.Producer.TeamSeasonDocumentProcessor));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Home team franchise season not found for {homeTeam.Ref} in command {command.CorrelationId}");
            }
            contest.HomeTeamFranchiseSeasonId = homeTeamFranchiseSeasonId.Value;

            /* Away Team */
            var awayTeam = externalDto
                .Competitions.First()
                .Competitors.First(x => x.HomeAway.ToLowerInvariant() == "away");

            var awayTeamFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                awayTeam.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (awayTeamFranchiseSeasonId == null)
            {
                var awayFranchiseUri = EspnUriMapper.TeamSeasonToFranchiseRef(awayTeam.Team.Ref);
                var awayFranchiseIdentity = _externalRefIdentityGenerator.Generate(awayFranchiseUri);

                // request sourcing
                await _publishEndpoint.Publish(new DocumentRequested(
                    awayTeam.Team.Ref.ToCleanUrl(),
                    awayFranchiseIdentity.CanonicalId.ToString(),
                    awayTeam.Team.Ref,
                    command.Sport,
                    command.Season,
                    DocumentType.TeamSeason,
                    command.SourceDataProvider,
                    command.CorrelationId,
                    CausationId.Producer.TeamSeasonDocumentProcessor));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Away team franchise season not found for {awayTeam.Ref} in command {command.CorrelationId}");
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
            Contest entity)
        {
            // TODO: Implement update logic if necessary
            await Task.Delay(100);
        }
    }
}
