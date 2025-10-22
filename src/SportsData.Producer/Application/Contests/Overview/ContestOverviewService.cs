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
            // 1) Resolve home/away FranchiseSeasonIds for this contest.
            var ids = await _dbContext.Contests
                .AsNoTracking()
                .Where(c => c.Id == contestId)
                .Select(c => new
                {
                    Home = c.HomeTeamFranchiseSeasonId,
                    Away = c.AwayTeamFranchiseSeasonId
                })
                .FirstOrDefaultAsync();

            if (ids is null)
                return new GameLeadersDto { Categories = new List<LeaderCategoryDto>() };

            // 2) Pull a flat list of leader rows (EF-friendly). No Include/ThenInclude needed.
            var flat = await _dbContext.CompetitionLeaders
                .AsNoTracking()
                .Where(l => l.Competition.ContestId == contestId)
                .SelectMany(l => l.Stats.Select(s => new FlatLeaderRow
                {
                    CategoryId = l.LeaderCategory.Name,
                    CategoryName = l.LeaderCategory.DisplayName ?? l.LeaderCategory.Name,
                    Abbr = null,   // map if you have l.LeaderCategory.Abbreviation
                    Unit = null,   // map if you have l.LeaderCategory.Unit
                    DisplayOrder = 0,      // map if you have l.LeaderCategory.DisplayOrder

                    FranchiseSeasonId = s.FranchiseSeasonId,
                    PlayerName =
                        (s.AthleteSeason != null && s.AthleteSeason.Athlete != null
                            ? (s.AthleteSeason.Athlete.ShortName ?? s.AthleteSeason.Athlete.DisplayName)
                            : null)
                        ?? "Unknown"
                    ,
                    StatLine = s.DisplayValue,
                    Numeric = null,   // map if you have a numeric primary value
                    Rank = 1       // map if you have s.Rank (lower = better)
                }))
                .ToListAsync();

            // 3) Group by category and build category-centric leaders with home/away arrays (ties allowed).
            var categories = flat
                .GroupBy(r => new { r.CategoryId, r.CategoryName, r.Abbr, r.Unit, r.DisplayOrder })
                .Select(g =>
                {
                    var homeStats = g.Where(r => r.FranchiseSeasonId == ids.Home);
                    var awayStats = g.Where(r => r.FranchiseSeasonId == ids.Away);

                    var homeLeaders = SelectTopWithTies(homeStats);
                    var awayLeaders = SelectTopWithTies(awayStats);

                    return new LeaderCategoryDto
                    {
                        CategoryId = g.Key.CategoryId,
                        CategoryName = g.Key.CategoryName,
                        Abbr = g.Key.Abbr,
                        Unit = g.Key.Unit,
                        DisplayOrder = g.Key.DisplayOrder,
                        Home = new TeamLeadersDto { Leaders = homeLeaders },
                        Away = new TeamLeadersDto { Leaders = awayLeaders }
                    };
                })
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.CategoryName)
                .ToList();

            return new GameLeadersDto { Categories = categories };
        }

        /// <summary>
        /// Strongly-typed projection row used for grouping/selection (EF-safe).
        /// </summary>
        private sealed class FlatLeaderRow
        {
            // Category metadata
            public string CategoryId { get; set; } = null!;
            public string CategoryName { get; set; } = null!;
            public string? Abbr { get; set; }
            public string? Unit { get; set; }
            public int DisplayOrder { get; set; }

            // Player/leader data
            public Guid FranchiseSeasonId { get; set; }
            public string PlayerName { get; set; } = null!;
            public string? StatLine { get; set; }
            public decimal? Numeric { get; set; }
            public int Rank { get; set; } = 1;
        }

        /// <summary>
        /// Picks the top leader(s) from a sequence, allowing ties.
        /// If you later map Rank/Numeric, the ordering and tie rule will be stronger.
        /// </summary>
        private static List<PlayerLeaderDto> SelectTopWithTies(IEnumerable<FlatLeaderRow> rows)
        {
            var list = rows.ToList();
            if (list.Count == 0) return new List<PlayerLeaderDto>();

            // Order: lower Rank first, then higher Numeric, then PlayerName (stable).
            var ordered = list
                .OrderBy(r => r.Rank)
                .ThenByDescending(r => r.Numeric ?? decimal.MinValue)
                .ThenBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var top = ordered[0];

            // Tie rule:
            //  1) If Numeric exists, tie on equal Numeric
            //  2) Else fall back to equal StatLine (case-insensitive)
            IEnumerable<FlatLeaderRow> winners = top.Numeric.HasValue
                ? ordered.Where(r => r.Numeric == top.Numeric)
                : ordered.Where(r => string.Equals(r.StatLine, top.StatLine, StringComparison.OrdinalIgnoreCase));

            // Map to PlayerLeaderDto (extend when you have PlayerId, Position, Jersey, Headshot, etc.).
            return winners.Select(w => new PlayerLeaderDto
            {
                PlayerId = null,        // map if available
                PlayerName = w.PlayerName,
                Position = null,        // map if available
                Jersey = null,        // map if available
                TeamId = null,        // map if useful
                Value = w.Numeric,
                StatLine = w.StatLine,
                Rank = 1,
                HeadshotUrl = null
            }).ToList();
        }

        private async Task<WinProbabilityDto> GetWinProbabilityAsync(
            Guid contestId,
            string awayTeamSlug,
            string homeTeamSlug,
            string awayTeamColor,
            string homeTeamColor)
        {
            var rows = await _dbContext.CompetitionProbabilities
                .AsNoTracking()
                .Where(p => p.Competition.ContestId == contestId)
                .OrderBy(p => p.SequenceNumber)
                .Select(p => new
                {
                    p.HomeWinPercentage,                // 0..1
                    p.AwayWinPercentage,                // 0..1
                    GameClock = p.Play != null ? p.Play.ClockDisplayValue : null,
                    Quarter = p.Play != null ? (int?)p.Play.PeriodNumber : null
                })
                .ToListAsync();

            static int ToPct(double? v) => v.HasValue ? (int)Math.Round(v.Value * 100) : 0;

            var points = rows.Select(r => new WinProbabilityPointDto
            {
                GameClock = r.GameClock,
                Quarter = r.Quarter.HasValue ? r.Quarter.Value : 0,
                HomeWinPercent = ToPct(r.HomeWinPercentage),
                AwayWinPercent = ToPct(r.AwayWinPercentage)
            }).ToList();

            var last = rows.Count > 0 ? rows[^1] : null;

            return new WinProbabilityDto
            {
                AwayTeamSlug = awayTeamSlug,
                HomeTeamSlug = homeTeamSlug,
                AwayTeamColor = awayTeamColor,
                HomeTeamColor = homeTeamColor,
                Points = points,
                FinalHomeWinPercent = last != null ? ToPct(last.HomeWinPercentage) : 0,
                FinalAwayWinPercent = last != null ? ToPct(last.AwayWinPercentage) : 0
            };
        }

        private async Task<PlayLogDto> GetPlayLogAsync(
            Guid contestId,
            string awayTeamSlug,
            string homeTeamSlug,
            Uri? awayTeamLogoUri,
            Uri? homeTeamLogoUri,
            Guid awayTeamFranchiseSeasonId,
            Guid homeTeamFranchiseSeasonId)
        {
            var plays = await _dbContext.CompetitionPlays
                .AsNoTracking()
                .Include(p => p.Competition)
                .Where(p => p.Competition.ContestId == contestId)
                .OrderBy(p => p.SequenceNumber)
                .ToListAsync();

            var playDtos = plays.Select((p,x) => new PlayDto
            {
                Ordinal = x,
                Quarter = p.PeriodNumber,
                FranchiseSeasonId = p.StartFranchiseSeasonId.HasValue ? p.StartFranchiseSeasonId.Value : Guid.Empty,
                Team = p.StartFranchiseSeasonId == awayTeamFranchiseSeasonId ? awayTeamSlug : homeTeamSlug,
                Description = p.Text,
                TimeRemaining = p.ClockDisplayValue,
                IsScoringPlay = p.ScoringPlay,
                IsKeyPlay = p.Priority
            }).ToList();

            return new PlayLogDto()
            {
                AwayTeamSlug = awayTeamSlug,
                HomeTeamSlug = homeTeamSlug,
                AwayTeamLogoUrl = awayTeamLogoUri is null ? string.Empty : awayTeamLogoUri.OriginalString,
                HomeTeamLogoUrl = homeTeamLogoUri is null ? string.Empty : homeTeamLogoUri.OriginalString,
                Plays = playDtos
            };
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
            return await _dbContext.Contests
                .AsNoTracking()
                .Where(c => c.Id == contestId)
                .Select(c => new GameInfoDto
                {
                    StartDateUtc = c.StartDateUtc,
                    Broadcast = string.Empty,
                    Venue = c.Venue != null ? c.Venue.Name : null,
                    VenueCity = c.Venue != null ? c.Venue.City : null,
                    VenueState = c.Venue != null ? c.Venue.State : null,
                    VenueImageUrl = c.Venue != null
                        ? c.Venue.Images
                            .OrderBy(i => i.CreatedUtc)
                            .Select(i => i.Uri.OriginalString)
                            .FirstOrDefault()
                        : null,
                    Attendance = c.Competitions
                        .OrderBy(comp => comp.Date)
                        .Select(comp => comp.Attendance)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<ContestOverviewDto> GetContestOverviewByContestId(Guid contestId)
        {
            // Fetch basic contest info to get team slugs and franchise season IDs
            var contest = await _dbContext.Contests
                .AsNoTracking()
                .Include(x => x.Competitions)
                .Include(x => x.AwayTeamFranchiseSeason!)
                .ThenInclude(x => x.Franchise)
                .ThenInclude(x => x.Logos)
                .Include(x => x.HomeTeamFranchiseSeason!)
                .ThenInclude(x => x.Franchise)
                .ThenInclude(x => x.Logos)
                .Include(x => x.AwayTeamFranchiseSeason!)
                .ThenInclude(x => x.Logos!)
                .Include(x => x.HomeTeamFranchiseSeason!)
                .ThenInclude(x => x.Logos!)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.Id == contestId);

            if (contest is null)
                throw new ArgumentException($"Contest with ID {contestId} not found.");

            var awayTeamSlug = contest.AwayTeamFranchiseSeason!.Franchise!.Slug!;
            var homeTeamSlug = contest.HomeTeamFranchiseSeason!.Franchise!.Slug!;

            var awayTeamColor = contest.AwayTeamFranchiseSeason!.Franchise.ColorCodeHex;
            var homeTeamColor = contest.HomeTeamFranchiseSeason!.Franchise.ColorCodeHex;

            var awayTeamLogoUri = contest.AwayTeamFranchiseSeason?.Logos?.FirstOrDefault()?.Uri;
            var homeTeamLogoUri = contest.HomeTeamFranchiseSeason?.Logos?.FirstOrDefault()?.Uri;

            var awayTeamFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId;
            var homeTeamFranchiseSeasonId = contest.HomeTeamFranchiseSeasonId;

            var dto = new ContestOverviewDto
            {
                Header = await GetGameHeaderAsync(contestId),
                Leaders = await GetGameLeadersAsync(contestId),
                WinProbability = await GetWinProbabilityAsync(
                    contestId,
                    awayTeamSlug,
                    homeTeamSlug,
                    awayTeamColor,
                    homeTeamColor
                    ),
                PlayLog = await GetPlayLogAsync(
                    contestId,
                    awayTeamSlug,
                    homeTeamSlug,
                    awayTeamLogoUri,
                    homeTeamLogoUri,
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
