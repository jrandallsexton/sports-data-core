using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
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
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideProviders _provider;

        public EventCompetitionDocumentProcessor(
            ILogger<EventDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus publishEndpoint,
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
                .Include(c => c.Competitors)
                .ThenInclude(c => c.ExternalIds)
                .AsSplitQuery()
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

            var venueId = await _dataContext.ResolveIdAsync<
                Venue, VenueExternalId>(
                venue,
                command.SourceDataProvider,
                () => _dataContext.Venues,
                externalIdsNav: "ExternalIds",
                key: v => v.Id);

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
                    Uri: venue.Ref.ToCleanUri(),
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
                    Uri: competitorDto.Ref.ToCleanUri(),
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

        private async Task<bool> ProcessSituation(
            ProcessDocumentCommand command,
            EspnEventCompetitionDto externalDto,
            Competition competition)
        {
            if (externalDto.Situation?.Ref is not null)
            {
                var situationIdentity = _externalRefIdentityGenerator.Generate(externalDto.Situation.Ref);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: situationIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(situationIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionSituation,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));

                return true;
            }

            return false;
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
                Uri: externalDto.Odds.Ref.ToCleanUri(),
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

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(externalDto.Broadcasts.Ref),
                ParentId: competition.Id.ToString(),
                Uri: externalDto.Broadcasts.Ref.ToCleanUri(),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionBroadcast,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
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
                Uri: externalDto.Details.Ref.ToCleanUri(),
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
                Uri: externalDto.Leaders.Ref.ToCleanUri(),
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
                Uri: externalDto.Predictor.Ref.ToCleanUri(),
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
                Uri: externalDto.Probabilities.Ref.ToCleanUri(),
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
                Uri: externalDto.PowerIndexes.Ref.ToCleanUri(),
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
                Uri: externalDto.Drives.Ref.ToCleanUri(),
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
            EspnEventCompetitionDto dto,
            Competition competition)
        {
            var raiseEvents = false;

            var updatedEntity = dto.AsEntity(
                _externalRefIdentityGenerator,
                competition.ContestId,
                command.CorrelationId);

            if (competition.Attendance != updatedEntity.Attendance)
            {
                competition.Attendance = updatedEntity.Attendance;
            }

            if (competition.Date != updatedEntity.Date)
            {
                competition.Date = updatedEntity.Date;

                await _publishEndpoint.Publish(
                    new ContestStartTimeUpdated(
                        competition.ContestId,
                        competition.Date,
                        command.CorrelationId,
                        CausationId.Producer.EventCompetitionDocumentProcessor));
                raiseEvents = true;
            }

            raiseEvents = raiseEvents || await ProcessSituation(command, dto, competition);

            if (dto.Status?.Ref is not null)
            {
                var statusIdentity = _externalRefIdentityGenerator.Generate(dto.Status.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: statusIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(statusIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionStatus,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            // update spreads, moneylines, totals, etc.
            if (dto.Odds?.Ref is not null)
            {
                var oddsIdentity = _externalRefIdentityGenerator.Generate(dto.Odds.Ref);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: oddsIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(oddsIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionOdds,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));

                raiseEvents = true;
            }

            if (dto.Broadcasts?.Ref is not null)
            {
                var broadcastIdentity = _externalRefIdentityGenerator.Generate(dto.Broadcasts.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: broadcastIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(broadcastIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionBroadcast,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.Details?.Ref is not null)
            {
                var playLogIdentity = _externalRefIdentityGenerator.Generate(dto.Details.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: playLogIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(playLogIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionPlay,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.Leaders?.Ref is not null)
            {
                var leadersIdentity = _externalRefIdentityGenerator.Generate(dto.Leaders.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: leadersIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(leadersIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionLeaders,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.Predictor?.Ref is not null)
            {
                var predictionIdentity = _externalRefIdentityGenerator.Generate(dto.Predictor.Ref);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: predictionIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(predictionIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionPrediction,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.Probabilities?.Ref is not null)
            {
                var probabilityIdentity = _externalRefIdentityGenerator.Generate(dto.Probabilities.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: probabilityIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(probabilityIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionProbability,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.PowerIndexes?.Ref is not null)
            {
                var powerIndexIdentity = _externalRefIdentityGenerator.Generate(dto.PowerIndexes.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: powerIndexIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(powerIndexIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionPowerIndex,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            if (dto.Drives?.Ref is not null)
            {
                var drivesIdentity = _externalRefIdentityGenerator.Generate(dto.Drives.Ref);
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: drivesIdentity.UrlHash,
                    ParentId: competition.Id.ToString(),
                    Uri: new Uri(drivesIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionDrive,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                ));
                raiseEvents = true;
            }

            foreach (var competitor in dto.Competitors)
            {
                var competitorIdentity = _externalRefIdentityGenerator.Generate(competitor.Ref);

                if (competitor.Score?.Ref is not null)
                {
                    var scoreIdentity = _externalRefIdentityGenerator.Generate(competitor.Score.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: scoreIdentity.UrlHash,
                        ParentId: competition.Id.ToString(),
                        Uri: new Uri(scoreIdentity.CleanUrl),
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.EventCompetitionCompetitorScore,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                    ));
                    raiseEvents = true;
                }

                if (competitor.Linescores?.Ref is not null)
                {
                    
                    var lineScoreIdentity = _externalRefIdentityGenerator.Generate(competitor.Linescores.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: lineScoreIdentity.UrlHash,
                        ParentId: competitorIdentity.CanonicalId.ToString(),
                        Uri: new Uri(lineScoreIdentity.CleanUrl),
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.EventCompetitionCompetitorLineScore,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                    ));
                    raiseEvents = true;
                }

                if (competitor.Statistics?.Ref is not null)
                {
                    var statisticsIdentity = _externalRefIdentityGenerator.Generate(competitor.Statistics.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: statisticsIdentity.UrlHash,
                        ParentId: competition.Id.ToString(),
                        Uri: new Uri(statisticsIdentity.CleanUrl),
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.EventCompetitionCompetitorStatistics,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                    ));
                    raiseEvents = true;
                }

                //if (competitor.Leaders?.Ref is not null)
                //{
                //    var leadersIdentity = _externalRefIdentityGenerator.Generate(competitor.Leaders.Ref);
                //    await _publishEndpoint.Publish(new DocumentRequested(
                //        Id: leadersIdentity.UrlHash,
                //        ParentId: competition.Id.ToString(),
                //        Uri: new Uri(leadersIdentity.CleanUrl),
                //        Sport: command.Sport,
                //        SeasonYear: command.Season,
                //        DocumentType: DocumentType.EventCompetitionLeaders,
                //        SourceDataProvider: command.SourceDataProvider,
                //        CorrelationId: command.CorrelationId,
                //        CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
                //    ));
                //}
            }

            if (raiseEvents)
            {
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();
            }
        }
    }
}
