using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
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
        private readonly IAppMode _appMode;

        public EnrichFranchiseSeasonHandler(
            ILogger<EnrichFranchiseSeasonHandler<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus eventBus,
            IAppMode appMode)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
            _appMode = appMode;
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

            // EVERY contest for the team, not just the finalized ones counted
            // above: the API's league cards show this team's record on games
            // not yet played, and those are the rows the event must reach.
            var allContestIds = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId ||
                            c.HomeTeamFranchiseSeasonId == command.FranchiseSeasonId)
                .Select(c => c.Id)
                .ToListAsync();

            await _eventBus.Publish(new FranchiseSeasonEnrichmentCompleted(
                command.FranchiseSeasonId,
                null,
                _appMode.CurrentSport,
                command.SeasonYear,
                command.CorrelationId,
                Guid.NewGuid(),
                allContestIds));

            await _dataContext.SaveChangesAsync();
        }

        private async Task<List<ContestBase>> GetFinalizedContestsForFranchiseSeason(Guid franchiseSeasonId)
        {
            // Preseason excluded (SeasonPhase TypeCode 1), matching the
            // preview-history queries' convention: a NULL/unmatched phase is
            // KEPT. Without this, NFL preseason finals counted into the W/L
            // this enrichment writes onto FranchiseSeason — the record every
            // league matchup card displays (prod 2026-09-09: Saints "1-2"
            // after week 1). NCAAFB never surfaced it: no preseason games.
            return await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.FinalizedUtc != null &&
                            (c.AwayTeamFranchiseSeasonId == franchiseSeasonId ||
                             c.HomeTeamFranchiseSeasonId == franchiseSeasonId))
                .Where(c => !_dataContext.SeasonPhases.Any(sp => sp.Id == c.SeasonPhaseId && sp.TypeCode == 1))
                .ToListAsync();
        }

        private async Task UpdateWinsAndLosses(
            EnrichFranchiseSeasonCommand command,
            FranchiseSeason franchiseSeason,
            List<ContestBase> contests)
        {
            var wins = 0;
            var losses = 0;
            var ties = 0;
            var conferenceWins = 0;
            var conferenceLosses = 0;
            var conferenceTies = 0;

            // Prefetch all opponent franchise seasons in a single query to avoid N+1
            var opponentIds = contests
                .Select(c => c.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId
                    ? c.HomeTeamFranchiseSeasonId
                    : c.AwayTeamFranchiseSeasonId)
                .Distinct()
                .ToList();

            // A conference record spans divisions: a team in SEC - West
            // (IsConference false) counts every SEC game, not only SEC West
            // ones. Compare on the team's group when it is the conference,
            // else on its parent when the parent is; a group with neither
            // flagged (NAIA, a division with no parent row) compares on itself.
            var conferenceIds = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(fs => fs.Id == franchiseSeason.Id || opponentIds.Contains(fs.Id))
                .Select(fs => new
                {
                    fs.Id,
                    ConferenceId = fs.GroupSeason != null &&
                                   !fs.GroupSeason.IsConference &&
                                   fs.GroupSeason.Parent != null &&
                                   fs.GroupSeason.Parent.IsConference
                        ? fs.GroupSeason.ParentId
                        : fs.GroupSeasonId
                })
                .ToDictionaryAsync(fs => fs.Id, fs => fs.ConferenceId);

            var conferenceId = conferenceIds[franchiseSeason.Id];

            // ESPN's conferenceCompetition flag is the authority where it is
            // published (NCAAFB 2014 on): it matches the official conference
            // record, which leaves out conference championship games. Where
            // it is false on every game (NCAAFB before 2014, NFL), fall back
            // to comparing conferences, dropping postseason games and
            // same-conference games noted as a championship (the NCAAFB title
            // game is a regular-season game; notes exist from 2003).
            //
            // A season uses the flag only when a real share of its games carry
            // it: NCAAFB 2008 has 2 stray flagged games of ~3,500, and "any
            // flagged game" would zero every other team's conference record.
            var seasonCompetitions = _dataContext.Competitions
                .AsNoTracking()
                .Where(k => k.Contest.SeasonYear == franchiseSeason.SeasonYear);

            var seasonCompetitionCount = await seasonCompetitions.CountAsync();
            var seasonFlaggedCount = await seasonCompetitions.CountAsync(k => k.IsConferenceCompetition);

            var seasonHasConferenceFlags = seasonFlaggedCount > 0 &&
                                           seasonFlaggedCount * 10 >= seasonCompetitionCount;

            var contestIds = contests.Select(c => c.Id).ToList();

            // Contest:Competition is 1:1 in practice, but nothing enforces it;
            // a duplicate must not throw and abort the whole enrichment.
            var competitionFacts = (await _dataContext.Competitions
                    .AsNoTracking()
                    .Where(k => contestIds.Contains(k.ContestId))
                    .Select(k => new
                    {
                        k.ContestId,
                        k.IsConferenceCompetition,
                        IsChampionship = k.Notes.Any(n => n.Headline.ToLower().Contains("championship"))
                    })
                    .ToListAsync())
                .DistinctBy(k => k.ContestId)
                .ToDictionary(k => k.ContestId);

            // SeasonPhase TypeCode 3 = postseason
            var phaseIds = contests.Select(c => c.SeasonPhaseId).Distinct().ToList();

            var postseasonPhaseIds = (await _dataContext.SeasonPhases
                    .AsNoTracking()
                    .Where(sp => phaseIds.Contains(sp.Id) && sp.TypeCode == 3)
                    .Select(sp => sp.Id)
                    .ToListAsync())
                .ToHashSet();

            foreach (var contest in contests)
            {
                var isTie = contest.WinnerFranchiseSeasonId == null;
                var wasWinner = !isTie && contest.WinnerFranchiseSeasonId == franchiseSeason.Id;

                if (isTie)
                    ties++;
                else if (wasWinner)
                    wins++;
                else
                    losses++;

                var opponentFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId == command.FranchiseSeasonId
                    ? contest.HomeTeamFranchiseSeasonId
                    : contest.AwayTeamFranchiseSeasonId;

                if (!conferenceIds.TryGetValue(opponentFranchiseSeasonId, out var opponentConferenceId))
                {
                    _logger.LogError("Opponent FranchiseSeason could not be loaded. {@Command}", command);
                    continue;
                }

                competitionFacts.TryGetValue(contest.Id, out var facts);

                var isConferenceGame = seasonHasConferenceFlags
                    ? facts?.IsConferenceCompetition == true
                    : conferenceId == opponentConferenceId &&
                      !postseasonPhaseIds.Contains(contest.SeasonPhaseId) &&
                      facts?.IsChampionship != true;

                if (isConferenceGame)
                {
                    if (isTie)
                        conferenceTies++;
                    else if (wasWinner)
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

        private void UpdateScoringMargins(FranchiseSeason franchiseSeason, List<ContestBase> contests)
        {
            var scored = new List<int>();
            var allowed = new List<int>();
            var winMargins = new List<int>();
            var lossMargins = new List<int>();

            foreach (var contest in contests)
            {
                var isHome = contest.HomeTeamFranchiseSeasonId == franchiseSeason.Id;
                var isTie = contest.WinnerFranchiseSeasonId == null;
                var isWinner = !isTie && contest.WinnerFranchiseSeasonId == franchiseSeason.Id;

                var teamScore = isHome ? contest.HomeScore!.Value : contest.AwayScore!.Value;
                var opponentScore = isHome ? contest.AwayScore!.Value : contest.HomeScore!.Value;

                scored.Add(teamScore);
                allowed.Add(opponentScore);

                var margin = teamScore - opponentScore;

                if (isWinner)
                    winMargins.Add(margin);
                else if (!isTie)
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
