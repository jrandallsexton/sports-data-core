using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Franchises.Commands
{
    public interface IEnrichFranchiseSeasons
    {
        Task Process(EnrichFranchiseSeasonCommand command);
    }

    public class EnrichFranchiseSeasonHandler<TDataContext> :
        IEnrichFranchiseSeasons where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EnrichFranchiseSeasonHandler<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _eventBus;

        public EnrichFranchiseSeasonHandler(
            ILogger<EnrichFranchiseSeasonHandler<TDataContext>> logger,
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

            var contests = await GetFinalizedContestsForFranchiseSeason(command.FranchiseSeasonId);

            await UpdateWinsAndLosses(command, franchiseSeason, contests);
            UpdateScoringMargins(franchiseSeason, contests);

            franchiseSeason.ModifiedUtc = DateTime.UtcNow;
            franchiseSeason.ModifiedBy = CausationId.Producer.FranchiseSeasonEnrichmentProcessor;

            await _dataContext.SaveChangesAsync();

            await _eventBus.Publish(new FranchiseSeasonEnrichmentCompleted(
                command.FranchiseSeasonId,
                null,
                Sport.FootballNcaa,
                command.SeasonYear,
                command.CorrelationId,
                Guid.NewGuid()));
        }

        private async Task<List<Contest>> GetFinalizedContestsForFranchiseSeason(Guid franchiseSeasonId)
        {
            return await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.FinalizedUtc != null &&
                            (c.AwayTeamFranchiseSeasonId == franchiseSeasonId ||
                             c.HomeTeamFranchiseSeasonId == franchiseSeasonId))
                .ToListAsync();
        }

        private async Task UpdateWinsAndLosses(
            EnrichFranchiseSeasonCommand command,
            FranchiseSeason franchiseSeason,
            List<Contest> contests)
        {
            var wins = 0;
            var losses = 0;
            var ties = 0;
            var conferenceWins = 0;
            var conferenceLosses = 0;
            var conferenceTies = 0;

            foreach (var contest in contests)
            {
                var wasWinner = contest.WinnerFranchiseId == franchiseSeason.FranchiseId;

                if (wasWinner)
                    wins++;
                else
                    losses++;

                var conferenceId = franchiseSeason.GroupSeasonId;

                var opponentFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId
                    ? contest.HomeTeamFranchiseSeasonId
                    : contest.AwayTeamFranchiseSeasonId;

                var oppFranchiseSeason = await _dataContext
                    .FranchiseSeasons
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == opponentFranchiseSeasonId);

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
                    else
                        conferenceLosses++;
                }
            }

            franchiseSeason.Wins = wins;
            franchiseSeason.Losses = losses;
            franchiseSeason.Ties = ties;

            franchiseSeason.ConferenceWins = conferenceWins;
            franchiseSeason.ConferenceLosses = conferenceLosses;
            franchiseSeason.ConferenceTies = conferenceTies;
        }

        private void UpdateScoringMargins(FranchiseSeason franchiseSeason, List<Contest> contests)
        {
            var scored = new List<int>();
            var allowed = new List<int>();
            var winMargins = new List<int>();
            var lossMargins = new List<int>();

            foreach (var contest in contests)
            {
                var isHome = contest.HomeTeamFranchiseSeasonId == franchiseSeason.Id;
                var isWinner = contest.WinnerFranchiseId == franchiseSeason.FranchiseId;

                var teamScore = isHome ? contest.HomeScore!.Value : contest.AwayScore!.Value;
                var opponentScore = isHome ? contest.AwayScore!.Value : contest.HomeScore!.Value;

                scored.Add(teamScore);
                allowed.Add(opponentScore);

                var margin = teamScore - opponentScore;

                if (isWinner)
                    winMargins.Add(margin);
                else
                    lossMargins.Add(Math.Abs(margin));
            }

            franchiseSeason.PtsScoredMin = scored.Any() ? scored.Min() : null;
            franchiseSeason.PtsScoredMax = scored.Any() ? scored.Max() : null;
            franchiseSeason.PtsScoredAvg = scored.Any()
                ? Math.Round(Convert.ToDecimal(scored.Average()), 2)
                : null;

            franchiseSeason.PtsAllowedMin = allowed.Any() ? allowed.Min() : null;
            franchiseSeason.PtsAllowedMax = allowed.Any() ? allowed.Max() : null;
            franchiseSeason.PtsAllowedAvg = allowed.Any()
                ? Math.Round(Convert.ToDecimal(allowed.Average()), 2)
                : null;

            franchiseSeason.MarginWinMin = winMargins.Any() ? winMargins.Min() : null;
            franchiseSeason.MarginWinMax = winMargins.Any() ? winMargins.Max() : null;
            franchiseSeason.MarginWinAvg = winMargins.Any()
                ? Math.Round(Convert.ToDecimal(winMargins.Average()), 2)
                : null;

            franchiseSeason.MarginLossMin = lossMargins.Any() ? lossMargins.Min() : null;
            franchiseSeason.MarginLossMax = lossMargins.Any() ? lossMargins.Max() : null;
            franchiseSeason.MarginLossAvg = lossMargins.Any()
                ? Math.Round(Convert.ToDecimal(lossMargins.Average()), 2)
                : null;
        }
    }
}
