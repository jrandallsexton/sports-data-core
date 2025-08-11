using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetition)]
    public class EventCompetitionDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideProviders _provider;

        public EventCompetitionDocumentProcessor(
            ILogger<EventDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideProviders provider
            )
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _provider = provider;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalDto = command.Document.FromJson<EspnEventCompetitionDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionDto Ref is null. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(command.ParentId))
            {
                _logger.LogError("ParentId not provided. Cannot process competition for null ContestId");
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var contestId))
            {
                _logger.LogError("Invalid ParentId format for ContestId. Cannot parse to Guid.");
                return;
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError("Command must have a SeasonYear defined");
                return;
            }

            var contest = await _dataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest is null)
            {
                _logger.LogError("Contest not found.");
                throw new InvalidOperationException($"Contest with ID {contestId} not found.");
            }

            var entity = await _dataContext.Competitions
                .Include(x => x.ExternalIds)
                .FirstOrDefaultAsync(x =>
                    x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                           z.Provider == command.SourceDataProvider));

            if (entity is null)
            {
                await ProcessNewEntity(
                    command,
                    externalDto,
                    command.Season.Value,
                    contestId);
            }
            else
            {
                await ProcessUpdate(
                    command,
                    externalDto,
                    entity);
            }
        }

        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            int seasonYear,
            Guid contestId)
        {
            var competition = externalDto.AsEntity(
                _externalRefIdentityGenerator,
                contestId,
                command.CorrelationId);

            await AddVenue(command, externalDto, competition);

            await ProcessCompetitors(command, externalDto, competition);

            ProcessNotes(command, externalDto, competition);

            await ProcessSituation(command, externalDto, competition);

            await ProcessStatus(command, externalDto, competition);

            await ProcessOdds(command, externalDto, competition);

            await ProcessBroadcasts(command, externalDto, competition);

            // shows as "Details" on the DTO, but is actually plays
            await ProcessPlays(command, externalDto, competition);

            await ProcessLeaders(command, externalDto, competition);

            ProcessLinks(command, externalDto, competition);

            await ProcessPredictions(command, externalDto, competition);

            await ProcessProbabilities(command, externalDto, competition);

            await ProcessPowerIndexes(command, externalDto, competition);

            await ProcessDrives(command, externalDto, competition);

            await _dataContext.Competitions.AddAsync(competition);
            await _dataContext.SaveChangesAsync();
        }

        private async Task AddVenue(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            var venue = externalDto.Venue;

            if (venue?.Ref is null)
            {
                _logger.LogWarning("No venue information provided in the competition document.");
                return;
            }

            var venueId = await _dataContext.TryResolveFromDtoRefAsync(
                venue,
                command.SourceDataProvider,
                () => _dataContext.Venues.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (venueId != null)
            {
                competition.VenueId = venueId.Value;
            }
            else
            {
                var venueHash = HashProvider.GenerateHashFromUri(venue.Ref);
                _logger.LogWarning("Venue not found for hash {VenueHash}, publishing sourcing request.", venueHash);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: venueHash,
                    ParentId: null,
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

        private async Task ProcessCompetitors(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            foreach (var competitorDto in externalDto.Competitors)
            {
                if (competitorDto?.Ref is null)
                {
                    _logger.LogError("Competitor reference is null, skipping competitor processing.");
                    continue;
                }

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: HashProvider.GenerateHashFromUri(competitorDto.Ref),
                    ParentId: competition.Id.ToString(),
                    Uri: competitorDto.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionCompetitor,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
            }
        }

        private static void ProcessNotes(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (!externalDto.Notes.Any())
            {
                return;
            }

            foreach (var note in externalDto.Notes)
            {
                var newNote = new CompetitionNote
                {
                    Type = note.Type,
                    Headline = note.Headline,
                    CompetitionId = competition.Id
                };
                competition.Notes.Add(newNote);
            }
        }

        private static async Task ProcessSituation(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            // TODO: Implement update logic if necessary
            await Task.Delay(100);
        }

        private async Task ProcessStatus(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Status?.Ref is null)
            {
                _logger.LogWarning("No status information provided in the competition document.");
                return;
            }

            // TODO: If a competition status is not final, we might want to launch a hangfire job to update it later
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Status.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Status.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionStatus,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessOdds(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Odds?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Odds.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Odds.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionOdds,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessBroadcasts(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Broadcasts?.Ref is null)
                return;

            var broadcastsResponse = await _provider.GetExternalDocument(
                new GetExternalDocumentQuery(
                    "",
                    externalDto.Broadcasts.Ref,
                    command.SourceDataProvider,
                    command.Sport,
                    DocumentType.EventCompetitionBroadcast,
                    command.Season));

            if (string.IsNullOrEmpty(broadcastsResponse.Data))
            {
                _logger.LogError("Broadcast unable to be sourced from Provider. {@BroadcastRef}", externalDto.Broadcasts?.Ref);
                return;
            }

            var broadcastsDto = broadcastsResponse.Data.FromJson<EspnEventCompetitionBroadcastDto>();
            if (broadcastsDto is null)
            {
                _logger.LogError("Failed to deserialize broadcasts document for {@BroadcastRef}", externalDto.Broadcasts.Ref);
                throw new InvalidOperationException("Deserialization of EspnEventCompetitionBroadcastDto failed.");
            }

            var existing = await _dataContext.Broadcasts
                .Where(b => b.CompetitionId == competition.Id)
                .ToListAsync();

            var existingKeys = new HashSet<string>(
                existing.Select(x => $"{x.TypeId}|{x.Channel}|{x.Slug}".ToLowerInvariant())
            );

            var newBroadcasts = broadcastsDto.Items
                .Where(item =>
                {
                    var key = $"{item.Type.Id}|{item.Channel}|{item.Slug}".ToLowerInvariant();
                    return !existingKeys.Contains(key);
                })
                .Select(item => item.AsEntity(competition.Id))
                .ToList();

            if (newBroadcasts.Count > 0)
            {
                _dataContext.Broadcasts.AddRange(newBroadcasts);
            }
        }

        private async Task ProcessPlays(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Details?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Details.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Details.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionPlay,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessLeaders(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Leaders?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Leaders.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Leaders.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionLeaders,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private static void ProcessLinks(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Links?.Any() != true)
                return;

            foreach (var link in externalDto.Links)
            {
                competition.Links.Add(new CompetitionLink()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competition.Id,
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

        private async Task ProcessPredictions(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Predictor?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Predictor.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Predictor.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionPrediction,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessProbabilities(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Probabilities?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Probabilities.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Probabilities.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionProbability,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessPowerIndexes(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.PowerIndexes?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.PowerIndexes.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.PowerIndexes.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionPowerIndex,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessDrives(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Drives?.Ref is null)
                return;

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Drives.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Drives.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionDrive,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
        }

        private async Task ProcessUpdate(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition entity)
        {
            _logger.LogError("Update was detected. Not implemented");
            await Task.Delay(100);
        }
    }
}
