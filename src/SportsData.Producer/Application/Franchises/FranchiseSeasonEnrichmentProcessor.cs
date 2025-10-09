using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Franchises
{
    public interface IEnrichFranchiseSeasons
    {
        Task Process(EnrichFranchiseSeasonCommand command);
    }

    public class FranchiseSeasonEnrichmentProcessor<TDataContext> :
        IEnrichFranchiseSeasons where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FranchiseSeasonEnrichmentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _eventBus;

        public FranchiseSeasonEnrichmentProcessor(
            ILogger<FranchiseSeasonEnrichmentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus eventBus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
        }

        public async Task Process(EnrichFranchiseSeasonCommand command)
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
                    _logger.LogError(ex, "Exception occurred while enriching franchise season. {@Command} {@StackTrace}", command, ex.StackTrace);
                    throw;
                }
            }
        }

        private async Task ProcessInternal(EnrichFranchiseSeasonCommand command)
        {
            var franchiseSeason = await _dataContext.FranchiseSeasons
                .Include(x => x.ExternalIds)
                .FirstOrDefaultAsync(x => x.Id == command.FranchiseSeasonId);

            if (franchiseSeason == null)
            {
                _logger.LogError("FranchiseSeason could not be found. {@Command}", command);
                return;
            }

            await UpdateWinsAndLosses(command, franchiseSeason);

            await RequestFranchiseSeasonSourcing(command, franchiseSeason, franchiseSeason.ExternalIds.First());
        }

        private async Task UpdateWinsAndLosses(
            EnrichFranchiseSeasonCommand command,
            FranchiseSeason franchiseSeason)
        {
            
            // update the wins and losses
            var contests = await _dataContext.Contests
                .Where(c => c.FinalizedUtc != null &&
                            (c.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId ||
                            c.HomeTeamFranchiseSeasonId == command.FranchiseSeasonId))
                .ToListAsync();

            var wins = 0;
            var losses = 0;
            var ties = 0; // TODO: Support ties for other sports after NCAAFB
            var conferenceWins = 0;
            var conferenceLosses = 0;
            var conferenceTies = 0;

            foreach (var contest in contests)
            {
                var wasWinner = contest.WinnerFranchiseId == command.FranchiseSeasonId;

                if (wasWinner)
                    wins++;

                if (!wasWinner)
                    losses++;

                // conference
                var conferenceId = franchiseSeason.GroupSeasonId;

                // determine the opponent's conference
                var opponentFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId ?
                    contest.HomeTeamFranchiseSeasonId :
                    contest.AwayTeamFranchiseSeasonId;

                // get the opponent's franchiseSeason
                var oppFranchiseSeason = await _dataContext
                    .FranchiseSeasons
                    .AsNoTracking()
                    .Where(x => x.Id == opponentFranchiseSeasonId)
                    .FirstOrDefaultAsync();

                if (oppFranchiseSeason is null)
                {
                    _logger.LogError("Opponent FranchiseSeason could not be loaded. {@Command}", command);
                    return;
                }

                var opponentConferenceId = oppFranchiseSeason.GroupSeasonId;

                if (conferenceId == opponentConferenceId)
                {
                    if (wasWinner)
                        conferenceWins++;

                    if (!wasWinner)
                        conferenceLosses++;
                }

                franchiseSeason.Wins = wins;
                franchiseSeason.Losses = losses;
                franchiseSeason.Ties = ties;

                franchiseSeason.ConferenceWins = conferenceWins;
                franchiseSeason.ConferenceLosses = conferenceLosses;
                franchiseSeason.ConferenceTies = conferenceTies;

                franchiseSeason.ModifiedUtc = DateTime.UtcNow;
                franchiseSeason.ModifiedBy = Guid.NewGuid();

                await _eventBus.Publish(
                    new FranchiseSeasonEnrichmentCompleted(
                        command.FranchiseSeasonId,
                        command.CorrelationId,
                        Guid.NewGuid()));

                await _dataContext.SaveChangesAsync();
            }
        }

        private async Task RequestFranchiseSeasonSourcing(
            EnrichFranchiseSeasonCommand command,
            FranchiseSeason franchiseSeason,
            FranchiseSeasonExternalId externalId)
        {
            await _eventBus.Publish(new DocumentRequested(
                Id: externalId.SourceUrlHash,
                ParentId: franchiseSeason.FranchiseId.ToString(),
                Uri: new Uri(externalId.SourceUrl),
                Sport: Sport.FootballNcaa,
                SeasonYear: command.SeasonYear,
                DocumentType: DocumentType.TeamSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.FranchiseSeasonEnrichmentProcessor
            ));
            await _dataContext.OutboxPings.AddAsync(new OutboxPing() { Id = Guid.NewGuid() });
            await _dataContext.SaveChangesAsync();
        }
    }
}
