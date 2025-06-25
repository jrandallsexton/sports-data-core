using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
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

        public EventDocumentProcessor(
            ILogger<EventDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
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

            // Add ESPN and internal external ID
            //contest.AddExternalId(command.SourceDataProvider, externalDto.Id);
            //contest.AddExternalId(SourceDataProvider.SportDeets, contestId.ToString());

            // Add contest links from dto.Links
            if (externalDto.Links?.Any() == true)
            {
                foreach (var link in externalDto.Links)
                {
                    contest.Links.Add(new ContestLink
                    {
                        Id = Guid.NewGuid(),
                        ContestId = contestId,
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

            // Attempt to resolve Venue from $ref
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

            // raise an event to source the competition
            foreach (var competition in externalDto.Competitions)
            {
                var competitionHash = HashProvider.GenerateHashFromUri(competition.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: competitionHash,
                    ParentId: contestId.ToString(),
                    Uri: competition.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetition,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventDocumentProcessor
                ));
            }

            await _dataContext.AddAsync(contest);

            await _publishEndpoint.Publish(new ContestCreated(
                contest.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));

            await _dataContext.SaveChangesAsync();
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
