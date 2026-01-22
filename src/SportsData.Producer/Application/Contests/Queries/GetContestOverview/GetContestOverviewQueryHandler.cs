using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests.Queries.GetContestOverview;

public interface IGetContestOverviewQueryHandler
{
    Task<Result<ContestOverviewDto>> ExecuteAsync(GetContestOverviewQuery query, CancellationToken cancellationToken = default);
}

public partial class GetContestOverviewQueryHandler : IGetContestOverviewQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ILogoSelectionService _logoSelectionService;

    public GetContestOverviewQueryHandler(
        TeamSportDataContext dbContext,
        ILogoSelectionService logoSelectionService)
    {
        _dbContext = dbContext;
        _logoSelectionService = logoSelectionService;
    }

    public async Task<Result<ContestOverviewDto>> ExecuteAsync(
        GetContestOverviewQuery query,
        CancellationToken cancellationToken = default)
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
            .FirstOrDefaultAsync(c => c.Id == query.ContestId, cancellationToken);

        if (contest is null)
        {
            return new Failure<ContestOverviewDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure("ContestId", $"Contest with ID {query.ContestId} not found")]);
        }

        var competitionId = contest.Competitions.First().Id;

        var awayTeamSlug = contest.AwayTeamFranchiseSeason!.Franchise!.Slug!;
        var homeTeamSlug = contest.HomeTeamFranchiseSeason!.Franchise!.Slug!;

        var awayTeamColor = contest.AwayTeamFranchiseSeason!.Franchise.ColorCodeHex;
        var homeTeamColor = contest.HomeTeamFranchiseSeason!.Franchise.ColorCodeHex;

        var awayTeamLogoUri = _logoSelectionService.SelectLogoForDarkBackground(contest.AwayTeamFranchiseSeason?.Logos);
        var homeTeamLogoUri = _logoSelectionService.SelectLogoForDarkBackground(contest.HomeTeamFranchiseSeason?.Logos);

        var awayTeamFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId;
        var homeTeamFranchiseSeasonId = contest.HomeTeamFranchiseSeasonId;

        var metrics = await GetCompetitionMetricsAsync(competitionId, awayTeamFranchiseSeasonId, homeTeamFranchiseSeasonId, cancellationToken);

        var dto = new ContestOverviewDto
        {
            Header = await GetGameHeaderAsync(query.ContestId, cancellationToken),
            Leaders = await GetGameLeadersAsync(query.ContestId, cancellationToken),
            WinProbability = await GetWinProbabilityAsync(
                query.ContestId,
                awayTeamSlug,
                homeTeamSlug,
                awayTeamColor,
                homeTeamColor,
                cancellationToken),
            PlayLog = await GetPlayLogAsync(
                query.ContestId,
                awayTeamSlug,
                homeTeamSlug,
                awayTeamLogoUri,
                homeTeamLogoUri,
                awayTeamFranchiseSeasonId,
                cancellationToken),
            TeamStats = await GetTeamStatsAsync(query.ContestId, cancellationToken),
            Info = await GetGameInfoAsync(query.ContestId, cancellationToken),
            AwayMetrics = metrics.Item1,
            HomeMetrics = metrics.Item2,
            MediaItems = await GetMedia(competitionId, cancellationToken)
        };

        return new Success<ContestOverviewDto>(dto);
    }

    private async Task<GameHeaderDto?> GetGameHeaderAsync(Guid contestId, CancellationToken cancellationToken)
    {
        var contest = await _dbContext.Contests
            .Include(c => c.Competitions)
            .Include(c => c.SeasonWeek)
            .Include(c => c.Venue)
            .FirstOrDefaultAsync(c => c.Id == contestId, cancellationToken);

        if (contest == null) return null;

        var homeTeamSeason = await _dbContext.FranchiseSeasons
            .Include(fs => fs.Franchise)
            .Include(fs => fs.Logos)
            .Include(fs => fs.GroupSeason)
            .FirstOrDefaultAsync(fs => fs.Id == contest.HomeTeamFranchiseSeasonId, cancellationToken);

        var awayTeamSeason = await _dbContext.FranchiseSeasons
            .Include(fs => fs.Franchise)
            .Include(fs => fs.Logos)
            .Include(fs => fs.GroupSeason)
            .FirstOrDefaultAsync(fs => fs.Id == contest.AwayTeamFranchiseSeasonId, cancellationToken);

        var quarterScores = await _dbContext.CompetitionCompetitorLineScores
            .Include(ls => ls.CompetitionCompetitor)
            .Where(ls => ls.CompetitionCompetitor.CompetitionId == contest.Competitions.First().Id)
            .OrderBy(ls => ls.Period)
            .ToListAsync(cancellationToken);

        var header = new GameHeaderDto
        {
            ContestId = contest.Id,
            Status = ContestStatus.Completed, // TODO: Map from actual status
            WeekLabel = contest.SeasonWeek.Number.ToString(),
            SeasonWeekId = contest.SeasonWeek.Id,
            SeasonYear = contest.SeasonYear,
            SeasonWeekNumber = contest.SeasonWeek.Number,
            StartTimeUtc = contest.StartDateUtc,
            VenueName = contest.Venue?.Name,
            Location = contest.Venue != null ? $"{contest.Venue.City}, {contest.Venue.State}" : null,
            HomeTeam = new TeamScoreDto
            {
                FranchiseSeasonId = contest.HomeTeamFranchiseSeasonId,
                DisplayName = homeTeamSeason?.Franchise?.Name,
                LogoUrl = _logoSelectionService.SelectLogoForDarkBackground(homeTeamSeason?.Logos)?.OriginalString,
                ColorPrimary = homeTeamSeason?.Franchise?.ColorCodeHex,
                FinalScore = contest.HomeScore,
                Slug = homeTeamSeason!.Franchise!.Slug,
                Conference = homeTeamSeason.GroupSeason?.ShortName,
                GroupSeasonMap = homeTeamSeason.GroupSeasonMap
            },
            AwayTeam = new TeamScoreDto
            {
                FranchiseSeasonId = contest.AwayTeamFranchiseSeasonId,
                DisplayName = awayTeamSeason?.Franchise?.Name,
                LogoUrl = _logoSelectionService.SelectLogoForDarkBackground(awayTeamSeason?.Logos)?.OriginalString,
                ColorPrimary = awayTeamSeason?.Franchise?.ColorCodeHex,
                FinalScore = contest.AwayScore,
                Slug = awayTeamSeason!.Franchise!.Slug,
                Conference = awayTeamSeason.GroupSeason?.ShortName,
                GroupSeasonMap = awayTeamSeason.GroupSeasonMap
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

    private async Task<GameLeadersDto> GetGameLeadersAsync(Guid contestId, CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(cancellationToken);

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
                Abbr = null,
                Unit = null,
                DisplayOrder = 0,
                FranchiseSeasonId = s.FranchiseSeasonId,
                PlayerName =
                    (s.AthleteSeason != null && s.AthleteSeason.Athlete != null
                        ? (s.AthleteSeason.Athlete.ShortName ?? s.AthleteSeason.Athlete.DisplayName)
                        : null)
                    ?? "Unknown",
                PlayerHeadshotUrl = null,
                StatLine = s.DisplayValue,
                Numeric = null,
                Rank = 1,
                AthleteSeasonId = s.AthleteSeasonId
            }))
            .ToListAsync(cancellationToken);

        // 2b) Fetch headshot URLs separately
        var athleteSeasonIds = flat.Select(f => f.AthleteSeasonId).Distinct().ToList();
        
        var headshots = await _dbContext.AthleteSeasons
            .AsNoTracking()
            .Where(a => athleteSeasonIds.Contains(a.Id))
            .Select(a => new
            {
                AthleteSeasonId = a.Id,
                HeadshotUrl = a.Athlete != null && a.Athlete.Images.Any()
                    ? a.Athlete.Images.OrderBy(i => i.CreatedUtc).First().Uri.ToString()
                    : null
            })
            .ToListAsync(cancellationToken);

        var headshotLookup = headshots.ToDictionary(h => h.AthleteSeasonId, h => h.HeadshotUrl);

        // Populate headshot URLs
        foreach (var row in flat)
        {
            if (headshotLookup.TryGetValue(row.AthleteSeasonId, out var headshotUrl))
            {
                row.PlayerHeadshotUrl = headshotUrl;
            }
        }

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

    private static List<PlayerLeaderDto> SelectTopWithTies(IEnumerable<FlatLeaderRow> rows)
    {
        var list = rows.ToList();
        if (list.Count == 0) return new List<PlayerLeaderDto>();

        var ordered = list
            .OrderBy(r => r.Rank)
            .ThenByDescending(r => r.Numeric ?? decimal.MinValue)
            .ThenBy(r => r.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var top = ordered[0];

        IEnumerable<FlatLeaderRow> winners = top.Numeric.HasValue
            ? ordered.Where(r => r.Numeric == top.Numeric)
            : ordered.Where(r => string.Equals(r.StatLine, top.StatLine, StringComparison.OrdinalIgnoreCase));

        return winners.Select(w => new PlayerLeaderDto
        {
            PlayerId = null,
            PlayerName = w.PlayerName,
            Position = null,
            Jersey = null,
            TeamId = null,
            Value = w.Numeric,
            StatLine = w.StatLine,
            Rank = 1,
            PlayerHeadshotUrl = w.PlayerHeadshotUrl
        }).ToList();
    }

    private async Task<WinProbabilityDto> GetWinProbabilityAsync(
        Guid contestId,
        string awayTeamSlug,
        string homeTeamSlug,
        string awayTeamColor,
        string homeTeamColor,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CompetitionProbabilities
            .Include(x => x.Play)
            .AsNoTracking()
            .Where(p => p.Competition.ContestId == contestId)
            .OrderBy(p => p.SequenceNumber)
            .Select(p => new
            {
                p.HomeWinPercentage,
                p.AwayWinPercentage,
                p.SequenceNumber,
                Play = p.Play != null
                    ? new
                    {
                        p.Play.ClockDisplayValue,
                        p.Play.PeriodNumber
                    }
                    : null
            })
            .ToListAsync(cancellationToken);

        var ordered = rows
            .OrderBy(p => int.TryParse(p.SequenceNumber, out var val) ? val : int.MaxValue)
            .ToList();

        static int ToPct(double? v) => v.HasValue ? (int)Math.Round(v.Value * 100) : 0;

        var points = ordered.Select(r => new WinProbabilityPointDto
        {
            GameClock = r.Play?.ClockDisplayValue,
            Quarter = r.Play?.PeriodNumber ?? 0,
            HomeWinPercent = ToPct(r.HomeWinPercentage),
            AwayWinPercent = ToPct(r.AwayWinPercentage)
        }).ToList();

        var last = ordered.LastOrDefault();

        return new WinProbabilityDto
        {
            AwayTeamSlug = awayTeamSlug,
            HomeTeamSlug = homeTeamSlug,
            AwayTeamColor = awayTeamColor,
            HomeTeamColor = homeTeamColor,
            Points = points,
            FinalHomeWinPercent = ToPct(last?.HomeWinPercentage),
            FinalAwayWinPercent = ToPct(last?.AwayWinPercentage)
        };
    }

    private async Task<PlayLogDto> GetPlayLogAsync(
        Guid contestId,
        string awayTeamSlug,
        string homeTeamSlug,
        Uri? awayTeamLogoUri,
        Uri? homeTeamLogoUri,
        Guid awayTeamFranchiseSeasonId,
        CancellationToken cancellationToken)
    {
        var plays = await _dbContext.CompetitionPlays
            .AsNoTracking()
            .Include(p => p.Competition)
            .Where(p => p.Competition.ContestId == contestId)
            .OrderBy(p => p.SequenceNumber)
            .ToListAsync(cancellationToken);

        var playDtos = plays.Select((p, x) => new PlayDto
        {
            Ordinal = x,
            Quarter = p.PeriodNumber,
            FranchiseSeasonId = p.EndFranchiseSeasonId.HasValue ? p.EndFranchiseSeasonId.Value : Guid.Empty,
            Team = p.EndFranchiseSeasonId == awayTeamFranchiseSeasonId ? awayTeamSlug : homeTeamSlug,
            Description = p.ShortAlternativeText ?? p.Text,
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

    private async Task<TeamStatsSectionDto> GetTeamStatsAsync(Guid contestId, CancellationToken cancellationToken)
    {
        var stats = await _dbContext.CompetitionCompetitorStatistics
            .Include(s => s.CompetitionCompetitor)
            .Where(s => s.CompetitionCompetitor!.Competition.ContestId == contestId)
            .ToListAsync(cancellationToken);

        return new TeamStatsSectionDto();
    }

    private async Task<GameInfoDto?> GetGameInfoAsync(Guid contestId, CancellationToken cancellationToken)
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
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<List<MediaItemDto>> GetMedia(Guid competitionId, CancellationToken cancellationToken)
    {
        var media = await _dbContext.CompetitionMedia
            .AsNoTracking()
            .Where(m => m.CompetitionId == competitionId)
            .OrderBy(m => m.CreatedUtc)
            .Select(m => new MediaItemDto
            {
                VideoId = m.VideoId,
                Title = m.Title,
                Description = m.Description,
                ChannelTitle = m.ChannelTitle,
                PublishedUtc = m.PublishedUtc,
                ThumbnailUrl = m.ThumbnailDefaultUrl,
                ThumbnailMediumUrl = m.ThumbnailMediumUrl,
                ThumbnailHighUrl = m.ThumbnailHighUrl
            })
            .ToListAsync(cancellationToken);

        return media;
    }

    private async Task<(CompetitionMetricDto?, CompetitionMetricDto?)> GetCompetitionMetricsAsync(
        Guid competitionId, 
        Guid awayFranchiseSeasonId, 
        Guid homeFranchiseSeasonId,
        CancellationToken cancellationToken)
    {
        var competitionMetrics = await _dbContext.CompetitionMetrics
            .Where(x => x.CompetitionId == competitionId)
            .ToListAsync(cancellationToken);

        if (competitionMetrics.Count != 2)
        {
            return (null, null);
        }

        var awayMetrics = competitionMetrics.First(x => x.FranchiseSeasonId == awayFranchiseSeasonId);
        var homeMetrics = competitionMetrics.First(x => x.FranchiseSeasonId == homeFranchiseSeasonId);

        return (awayMetrics.ToDto(), homeMetrics.ToDto());
    }
}