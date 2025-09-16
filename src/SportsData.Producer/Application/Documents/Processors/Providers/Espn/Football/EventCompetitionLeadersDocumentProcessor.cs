using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/leaders?lang=en
    /// </summary>
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionLeaders)]
    public class EventCompetitionLeadersDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionLeadersDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalIdentityGenerator;

        public EventCompetitionLeadersDocumentProcessor(
            ILogger<EventCompetitionLeadersDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus publishEndpoint,
            IGenerateExternalRefIdentities externalIdentityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
            _externalIdentityGenerator = externalIdentityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Processing EventCompetitionLeadersDocument with {@Command}", command);
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
            var dto = command.Document.FromJson<EspnEventCompetitionLeadersDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionLeadersDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionLeadersDto Ref is null. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(command.ParentId))
            {
                _logger.LogError("ParentId not provided. Cannot process competition leaders for null CompetitionId");
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("Invalid ParentId format for CompetitionId. Cannot parse to Guid.");
                return;
            }

            // Resolve parent Competition entity
            var competition = await _dataContext.Competitions
                .Include(x => x.ExternalIds)
                .Include(x => x.Leaders)
                    .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition is null)
            {
                _logger.LogError("Competition not found. {@Command}", command);
                return;
            }

            var franchiseSeasonCache = new Dictionary<string, Guid>();
            var athleteSeasonCache = new Dictionary<string, Guid>();

            foreach (var category in dto.Categories)
            {
                var leaderCategory = await _dataContext.LeaderCategories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Name == category.Name);

                if (leaderCategory == null)
                {
                    _logger.LogError("Leader category '{Category}' not found, skipping", category.Name);
                    continue;
                }

                var leaderEntity = category.AsEntity(
                    competitionId: competition.Id,
                    leaderCategoryId: leaderCategory.Id,
                    correlationId: command.CorrelationId);

                foreach (var leaderDto in category.Leaders)
                {
                    if (!athleteSeasonCache.TryGetValue(leaderDto.Athlete.Ref.ToString(), out var athleteSeasonId))
                    {
                        athleteSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                            leaderDto.Athlete,
                            command.SourceDataProvider,
                            () => _dataContext.AthleteSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                            _logger) ?? Guid.Empty;

                        athleteSeasonCache[leaderDto.Athlete.Ref.ToString()] = athleteSeasonId;
                    }

                    if (athleteSeasonId == Guid.Empty)
                    {
                        var athleteHash = HashProvider.GenerateHashFromUri(leaderDto.Athlete.Ref);
                        _logger.LogWarning("Athlete not found for hash {AthleteHash}, publishing sourcing request.", athleteHash);

                        await _publishEndpoint.Publish(new DocumentRequested(
                            Id: athleteHash,
                            ParentId: competition.Id.ToString(),
                            Uri: leaderDto.Athlete.Ref.ToCleanUri(),
                            Sport: command.Sport,
                            SeasonYear: command.Season,
                            DocumentType: DocumentType.Athlete,
                            SourceDataProvider: command.SourceDataProvider,
                            CorrelationId: command.CorrelationId,
                            CausationId: CausationId.Producer.EventCompetitionLeadersDocumentProcessor
                        ));
                        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                        await _dataContext.SaveChangesAsync();

                        // TODO: Continue processing and raise the document requested event at the end and throw a single exception
                        throw new InvalidOperationException($"Missing athlete for leader category '{category.Name}' - will retry later.");
                    }

                    if (!franchiseSeasonCache.TryGetValue(leaderDto.Team.Ref.ToString(), out var franchiseSeasonId))
                    {
                        franchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                            leaderDto.Team,
                            command.SourceDataProvider,
                            () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                            _logger) ?? Guid.Empty;

                        franchiseSeasonCache[leaderDto.Team.Ref.ToString()] = franchiseSeasonId;
                    }

                    if (franchiseSeasonId == Guid.Empty)
                    {
                        var teamHash = HashProvider.GenerateHashFromUri(leaderDto.Team.Ref);
                        _logger.LogWarning("FranchiseSeason not found for hash {TeamHash}, publishing sourcing request.", teamHash);

                        await _publishEndpoint.Publish(new DocumentRequested(
                            Id: teamHash,
                            ParentId: competition.Id.ToString(),
                            Uri: leaderDto.Team.Ref.ToCleanUri(),
                            Sport: command.Sport,
                            SeasonYear: command.Season,
                            DocumentType: DocumentType.TeamSeason,
                            SourceDataProvider: command.SourceDataProvider,
                            CorrelationId: command.CorrelationId,
                            CausationId: CausationId.Producer.EventCompetitionLeadersDocumentProcessor
                        ));
                        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                        await _dataContext.SaveChangesAsync();

                        throw new InvalidOperationException($"Missing franchise season for leader category '{category.Name}' - will retry later.");
                    }

                    // need to request sourcing for leader.Statistics
                    // this is the complete stats object for this athlete in this competition
                    var athleteSeasonIdentity = _externalIdentityGenerator.Generate(leaderDto.Athlete.Ref);
                    var athleteCompetitionStatsIdentity = _externalIdentityGenerator.Generate(leaderDto.Statistics.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: athleteCompetitionStatsIdentity.UrlHash,
                        ParentId: athleteSeasonIdentity.CanonicalId.ToString(),
                        Uri: new Uri(athleteCompetitionStatsIdentity.CleanUrl),
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.EventCompetitionAthleteStatistics,
                        SourceDataProvider: command.SourceDataProvider,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventCompetitionLeadersDocumentProcessor
                    ));

                    var stat = leaderDto.AsEntity(
                        parentLeaderId: leaderEntity.Id,
                        athleteSeasonId: athleteSeasonId,
                        franchiseSeasonId: franchiseSeasonId,
                        correlationId: command.CorrelationId);

                    //leaderEntity.Stats.Add(stat);
                    await _dataContext.CompetitionLeaderStats.AddAsync(stat);
                }

                if (leaderEntity.Stats.Count > 0)
                {
                    await _dataContext.CompetitionLeaders.AddAsync(leaderEntity);
                }
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
