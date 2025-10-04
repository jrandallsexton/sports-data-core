using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Overview
{
    public interface IContestOverviewService
    {
        Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId);
    }

    public class ContestOverviewService : IContestOverviewService
    {
        private readonly TeamSportDataContext _dbContext;

        public ContestOverviewService(
            TeamSportDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        private async Task<GameHeaderDto?> GetGameHeaderAsync(Guid contestId)
        {
            var contest = await _dbContext.Contests
                .Include(c => c.Competitions)
                .Include(c => c.SeasonWeek)
                .Include(c => c.Venue)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest == null) return null;

            var homeTeamSeason = await _dbContext.FranchiseSeasons
                .Include(fs => fs.Franchise)
                .Include(fs => fs.Logos)
                .FirstOrDefaultAsync(fs => fs.Id == contest.HomeTeamFranchiseSeasonId);

            var awayTeamSeason = await _dbContext.FranchiseSeasons
                .Include(fs => fs.Franchise)
                .Include(fs => fs.Logos)
                .FirstOrDefaultAsync(fs => fs.Id == contest.AwayTeamFranchiseSeasonId);

            var quarterScores = await _dbContext.CompetitionCompetitorLineScores
                .Include(ls => ls.CompetitionCompetitor)
                .Where(ls => ls.CompetitionCompetitor.CompetitionId == contest.Competitions.First().Id)
                .OrderBy(ls => ls.Period)
                .ToListAsync();

            var header = new GameHeaderDto
            {
                ContestId = contest.Id,
                Status = ContestStatus.Completed, // TODO: Map from actual status
                WeekLabel = contest.SeasonWeek?.Number.ToString(),
                StartTimeUtc = contest.StartDateUtc,
                VenueName = contest.Venue?.Name,
                Location = contest.Venue != null ? $"{contest.Venue.City}, {contest.Venue.State}" : null,
                HomeTeam = new TeamScoreDto
                {
                    FranchiseSeasonId = contest.HomeTeamFranchiseSeasonId,
                    DisplayName = homeTeamSeason?.Franchise?.Name,
                    LogoUrl = homeTeamSeason?.Logos?.FirstOrDefault()?.Uri.OriginalString,
                    ColorPrimary = homeTeamSeason?.Franchise?.ColorCodeHex,
                    FinalScore = contest.HomeScore
                },
                AwayTeam = new TeamScoreDto
                {
                    FranchiseSeasonId = contest.AwayTeamFranchiseSeasonId,
                    DisplayName = awayTeamSeason?.Franchise?.Name,
                    LogoUrl = awayTeamSeason?.Logos?.FirstOrDefault()?.Uri.OriginalString,
                    ColorPrimary = awayTeamSeason?.Franchise?.ColorCodeHex,
                    FinalScore = contest.AwayScore
                },
                QuarterScores = quarterScores
                    .GroupBy(ls => ls.Period)
                    .Select(g => new QuarterScoreDto
                    {
                        Quarter = g.Key,
                        HomeScore = g.FirstOrDefault(ls => ls.CompetitionCompetitor.HomeAway == "home")?.Value ?? 0,
                        AwayScore = g.FirstOrDefault(ls => ls.CompetitionCompetitor.HomeAway == "away")?.Value ?? 0
                    })
                    .ToList()
            };

            return header;
        }

        private async Task<GameLeadersDto> GetGameLeadersAsync(Guid contestId)
        {
            var leaders = await _dbContext.CompetitionLeaders
                .Include(l => l.Stats)
                .ThenInclude(s => s.AthleteSeason)
                .ThenInclude(a => a.Athlete)
                .Where(l => l.Competition.ContestId == contestId)
                .ToListAsync();

            return new GameLeadersDto
            {
                HomeLeaders = leaders
                    .Where(l => l.Stats.Any(s => s.FranchiseSeasonId == l.Competition.Contest.HomeTeamFranchiseSeasonId))
                    .Select(l => new StatLeaderDto
                    {
                        Category = l.LeaderCategory.Name,
                        PlayerName = l.Stats.FirstOrDefault()?.AthleteSeason.Athlete.DisplayName,
                        StatLine = l.Stats.FirstOrDefault()?.DisplayValue
                    })
                    .ToList(),
                AwayLeaders = leaders
                    .Where(l => l.Stats.Any(s => s.FranchiseSeasonId == l.Competition.Contest.AwayTeamFranchiseSeasonId))
                    .Select(l => new StatLeaderDto
                    {
                        Category = l.LeaderCategory.DisplayName,
                        PlayerName = l.Stats.FirstOrDefault()?.AthleteSeason.Athlete.DisplayName,
                        StatLine = l.Stats.FirstOrDefault()?.DisplayValue
                    })
                    .ToList()
            };
        }

        private async Task<WinProbabilityDto> GetWinProbabilityAsync(Guid contestId)
        {
            var probabilities = await _dbContext.CompetitionProbabilities
                .Where(p => p.Competition.ContestId == contestId)
                .OrderBy(p => p.CreatedUtc)
                .ToListAsync();

            return new WinProbabilityDto
            {
                Points = probabilities.Select(p => new WinProbabilityPointDto
                {
                    GameClock = p.Play?.ClockDisplayValue,
                    HomeWinPercent = (int)(p.HomeWinPercentage * 100),
                    AwayWinPercent = (int)(p.AwayWinPercentage * 100),
                    Quarter = p.Play!.PeriodNumber
                }).ToList(),
                FinalHomeWinPercent = (int)(probabilities.LastOrDefault()?.HomeWinPercentage * 100 ?? 0),
                FinalAwayWinPercent = (int)(probabilities.LastOrDefault()?.AwayWinPercentage * 100 ?? 0)
            };
        }

        private async Task<List<PlayDto>> GetPlayLogAsync(
            Guid contestId,
            string awayTeamSlug,
            string homeTeamSlug,
            Guid awayTeamFranchiseSeasonId,
            Guid homeTeamFranchiseSeasonId)
        {
            var plays = await _dbContext.CompetitionPlays
                .Include(p => p.Competition)
                .Where(p => p.Competition.ContestId == contestId)
                .OrderBy(p => p.SequenceNumber)
                .ToListAsync();

            return plays.Select((p,x) => new PlayDto
            {
                Ordinal = x,
                Quarter = p.PeriodNumber,
                FranchiseSeasonId = p.TeamFranchiseSeasonId,
                Team = p.TeamFranchiseSeasonId == awayTeamFranchiseSeasonId ? awayTeamSlug : homeTeamSlug,
                Description = p.Text,
                TimeRemaining = p.ClockDisplayValue,
                IsScoringPlay = p.ScoringPlay,
                IsKeyPlay = p.Priority
            }).ToList();
        }

        private async Task<TeamStatsSectionDto> GetTeamStatsAsync(Guid contestId)
        {
            var stats = await _dbContext.CompetitionCompetitorStatistics
                .Include(s => s.CompetitionCompetitor)
                .Where(s => s.CompetitionCompetitor!.Competition.ContestId == contestId)
                .ToListAsync();

            return new TeamStatsSectionDto
            {
                //HomeTeam = new TeamStatBlockDto
                //{
                //    Stats = stats
                //        .Where(s => s.CompetitionCompetitor.HomeAway == "home")
                //        .ToDictionary(s => s.Categories, s => s.Value.ToString())
                //},
                //AwayTeam = new TeamStatBlockDto
                //{
                //    Stats = stats
                //        .Where(s => s.CompetitionCompetitor.HomeAway == "away")
                //        .ToDictionary(s => s.Category, s => s.Value.ToString())
                //}
            };
        }

        private async Task<GameInfoDto?> GetGameInfoAsync(Guid contestId)
        {
            var contest = await _dbContext.Contests
                .AsNoTracking()
                .Include(c => c.Venue)
                .ThenInclude(v => v!.Images
                    .OrderBy(i => i.CreatedUtc)   // or .OrderByDescending(i => i.IsPrimary)
                    .Take(1))                     // 👈 allowed in Include
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest is null) return null;

            var img = contest.Venue?.Images?.FirstOrDefault();

            return new GameInfoDto
            {
                StartDateUtc = contest.StartDateUtc,
                Broadcast = string.Empty,
                Venue = contest.Venue?.Name,
                VenueCity = contest.Venue?.City,
                VenueState = contest.Venue?.State,
                VenueImageUrl = img?.Uri?.OriginalString,
                Attendance = 0
            };
        }

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId)
        {
            // Fetch basic contest info to get team slugs and franchise season IDs
            var contest = await _dbContext.Contests
                .Include(x => x.Competitions)
                .Include(x => x.AwayTeamFranchiseSeason!)
                .ThenInclude(x => x.Franchise)
                .Include(x => x.HomeTeamFranchiseSeason!)
                .ThenInclude(x => x.Franchise)
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest is null)
                throw new ArgumentException($"Contest with ID {contestId} not found.");

            var awayTeamSlug = contest.AwayTeamFranchiseSeason!.Franchise!.Slug!;
            var homeTeamSlug = contest.HomeTeamFranchiseSeason!.Franchise!.Slug!;

            var awayTeamFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId;
            var homeTeamFranchiseSeasonId = contest.HomeTeamFranchiseSeasonId;

            var dto = new ContestOverviewDto
            {
                Header = await GetGameHeaderAsync(contestId),
                Leaders = await GetGameLeadersAsync(contestId),
                WinProbability = await GetWinProbabilityAsync(contestId),
                PlayLog = await GetPlayLogAsync(
                    contestId,
                    awayTeamSlug,
                    homeTeamSlug,
                    awayTeamFranchiseSeasonId,
                    homeTeamFranchiseSeasonId),
                TeamStats = await GetTeamStatsAsync(contestId),
                Info = await GetGameInfoAsync(contestId)
            };

            //dto.Summary = await GetNarrativeSummaryAsync(contestId);

            //dto.MatchupAnalysis = await GetMatchupAnalysisAsync(contestId);

            return dto;
        }
    }
}
