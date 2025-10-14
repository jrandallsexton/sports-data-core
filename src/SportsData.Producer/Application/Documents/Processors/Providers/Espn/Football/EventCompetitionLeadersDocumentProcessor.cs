using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
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
            var dto = command.Document.FromJson<EspnEventCompetitionLeadersDto>();
            if (dto is null || string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("Invalid or null DTO in {@Command}", command);
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("Invalid or missing ParentId in {@Command}", command);
                return;
            }

            var competition = await _dataContext.Competitions
                .Include(x => x.ExternalIds)
                .Include(x => x.Leaders).ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition is null)
            {
                _logger.LogError("Competition not found. {@Command}", command);
                return;
            }

            // delete existing leaders & stats
            _dataContext.CompetitionLeaderStats.RemoveRange(
                competition.Leaders.SelectMany(l => l.Stats));
            _dataContext.CompetitionLeaders.RemoveRange(competition.Leaders);
            await _dataContext.SaveChangesAsync();

            var franchiseSeasonCache = new Dictionary<string, Guid>();
            var athleteSeasonCache = new Dictionary<string, Guid>();

            var leaders = new List<CompetitionLeader>();

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
                    competitionId,
                    leaderCategory.Id,
                    command.CorrelationId
                );

                foreach (var leaderDto in category.Leaders)
                {
                    if (leaderDto.Statistics?.Ref is null)
                    {
                        _logger.LogInformation("Leader statistics ref is null, skipping");
                        continue;
                    }

                    var athleteSeasonId = await ResolveAthleteSeasonIdAsync(leaderDto.Athlete, command, athleteSeasonCache);
                    var franchiseSeasonId = await ResolveFranchiseSeasonIdAsync(leaderDto.Team, command, franchiseSeasonCache);

                    var athleteSeasonIdentity = _externalIdentityGenerator.Generate(leaderDto.Athlete.Ref);
                    var statsIdentity = _externalIdentityGenerator.Generate(leaderDto.Statistics.Ref);

                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: statsIdentity.UrlHash,
                        ParentId: athleteSeasonIdentity.CanonicalId.ToString(),
                        Uri: new Uri(statsIdentity.CleanUrl),
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

                    leaderEntity.Stats.Add(stat);
                }

                leaders.Add(leaderEntity);
            }

            await _dataContext.CompetitionLeaders.AddRangeAsync(leaders);

            await _dataContext.SaveChangesAsync();
        }

        private async Task<Guid> ResolveAthleteSeasonIdAsync(
            IHasRef athleteDto,
            ProcessDocumentCommand command,
            Dictionary<string, Guid> cache)
        {
            var key = athleteDto.Ref.ToString();

            if (cache.TryGetValue(key, out var cachedId))
                return cachedId;

            var athleteSeasonIdentity = _externalIdentityGenerator.Generate(athleteDto.Ref);

            var athleteSeason = await _dataContext.AthleteSeasons
                .Include(x => x.ExternalIds)
                .Where(s => s.ExternalIds.Any(e =>
                    e.Provider == command.SourceDataProvider &&
                    e.Value == athleteSeasonIdentity.UrlHash))
                .FirstOrDefaultAsync();

            if (athleteSeason is null)
            {
                var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(athleteDto.Ref);
                var athleteIdentity = _externalIdentityGenerator.Generate(athleteRef);

                _logger.LogWarning("AthleteSeason not found for hash {Hash}, requesting source.", athleteSeasonIdentity.UrlHash);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: athleteSeasonIdentity.UrlHash,
                    ParentId: athleteIdentity.CanonicalId.ToString(),
                    Uri: new Uri(athleteSeasonIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.AthleteSeason,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionLeadersDocumentProcessor
                ));

                throw new ExternalDocumentNotSourcedException($"Missing AthleteSeason for ref {athleteDto.Ref}");
            }

            cache[key] = athleteSeason.Id;
            return athleteSeason.Id;
        }

        private async Task<Guid> ResolveFranchiseSeasonIdAsync(
            IHasRef teamDto,
            ProcessDocumentCommand command,
            Dictionary<string, Guid> cache)
        {
            var key = teamDto.Ref.ToString();

            if (cache.TryGetValue(key, out var cachedId))
                return cachedId;

            var resolvedId = await _dataContext.TryResolveFromDtoRefAsync(
                teamDto,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger
            );

            if (resolvedId is null)
            {
                var franchiseRef = EspnUriMapper.TeamSeasonToFranchiseRef(teamDto.Ref);
                var franchiseIdentity = _externalIdentityGenerator.Generate(franchiseRef);

                var franchiseSeasonIdentity = _externalIdentityGenerator.Generate(teamDto.Ref);
                
                _logger.LogWarning("FranchiseSeason not found for hash {Hash}, requesting source.", franchiseSeasonIdentity.UrlHash);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: franchiseSeasonIdentity.UrlHash,
                    ParentId: franchiseIdentity.CanonicalId.ToString(),
                    Uri: new Uri(franchiseSeasonIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.TeamSeason,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionLeadersDocumentProcessor
                ));

                throw new ExternalDocumentNotSourcedException($"Missing FranchiseSeason for ref {teamDto.Ref}");
            }

            cache[key] = resolvedId.Value;
            return resolvedId.Value;
        }
    }
}