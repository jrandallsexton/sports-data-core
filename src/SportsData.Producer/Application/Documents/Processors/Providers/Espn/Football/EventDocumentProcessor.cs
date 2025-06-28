using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
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
        private readonly IProvideProviders _providerClient;

        public EventDocumentProcessor(
            ILogger<EventDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
            _providerClient = providerClient;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Processing EventDocument with {@Command}", command);
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalDto = command.Document.FromJson<EspnEventDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventDto for event ID {@UrlHash}", command.UrlHash);
                throw new InvalidOperationException("EspnEventDto deserialization failed.");
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError("Command must have a SeasonYear defined");
                throw new InvalidOperationException("SeasonYear must be defined in the command.");
            }

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var entity = await _dataContext.Contests.FirstOrDefaultAsync(x =>
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
            var contestId = Guid.NewGuid();
            var contest = externalDto.AsEntity(command.Sport, seasonYear, contestId, command.CorrelationId);

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

                // TODO: Determine what to do with Situation

                // Get the status of the competition
                var statusResponse = await _providerClient.GetExternalDocument(
                    new GetExternalDocumentQuery(
                        contest.Id.ToString(),
                        competition.Status.Ref,
                        command.SourceDataProvider,
                        command.Sport,
                        DocumentType.EventCompetitionStatus,
                        command.Season));
                if (statusResponse.IsSuccess)
                {
                    var status = statusResponse.Data.FromJson<EspnEventCompetitionStatusDto>();
                    if (status is not null)
                    {
                        contest.Clock = status.Clock;
                        contest.DisplayClock = status.DisplayClock;
                        contest.Period = status.Period;

                        // TODO: update contest status based on competition status
                    }
                }

                // raise an event to source the odds
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: HashProvider.GenerateHashFromUri(competition.Ref),
                    ParentId: contest.Id.ToString(),
                    Uri: competition.Odds.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionOdds,
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
                    venue, command.SourceDataProvider, () => _dataContext.Venues, _logger);

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
            var homeTeam = externalDto
                .Competitions.First()
                .Competitors.First(x => x.HomeAway.ToLowerInvariant() == "home");
            var homeTeamFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                homeTeam, command.SourceDataProvider, () => _dataContext.FranchiseSeasons, _logger);
            if (homeTeamFranchiseSeasonId == null)
            {
                throw new InvalidOperationException(
                    $"Home team franchise season not found for {homeTeam.Ref} in command {command.CorrelationId}");
            }
            contest.HomeTeamFranchiseSeasonId = homeTeamFranchiseSeasonId.Value;

            var awayTeam = externalDto
                .Competitions.First()
                .Competitors.First(x => x.HomeAway.ToLowerInvariant() == "away");
            var awayTeamFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                awayTeam, command.SourceDataProvider, () => _dataContext.FranchiseSeasons, _logger);
            if (awayTeamFranchiseSeasonId == null)
            {
                throw new InvalidOperationException(
                    $"Away team franchise season not found for {awayTeam.Ref} in command {command.CorrelationId}");
            }
            contest.AwayTeamFranchiseSeasonId = awayTeamFranchiseSeasonId.Value;
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
