using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Processing;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.FranchiseSeasons
{
    public interface IFranchiseSeasonMetricsService
    {
        Task GenerateFranchiseSeasonMetrics(GenerateFranchiseSeasonMetricsCommand command);
        Task GenerateFranchiseSeasonMetrics(Guid franchiseSeasonId, int seasonYear);
        Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetricsBySeasonYear(int seasonYear);
        Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId);
    }

    public class FranchiseSeasonMetricsService : IFranchiseSeasonMetricsService
    {
        private readonly ILogger<FranchiseSeasonMetricsService> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IGroupSeasonsService _groupSeasonsService;

        public FranchiseSeasonMetricsService(
            ILogger<FranchiseSeasonMetricsService> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IGroupSeasonsService groupSeasonsService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _groupSeasonsService = groupSeasonsService;
        }

        public async Task<List<FranchiseSeasonMetricsDto>> GetFranchiseSeasonMetricsBySeasonYear(int seasonYear)
        {
            var metrics = await _dataContext.FranchiseSeasonMetrics
                .Include(fsm => fsm.FranchiseSeason)
                    .ThenInclude(fs => fs.Franchise)
                .Include(fsm => fsm.FranchiseSeason)
                    .ThenInclude(fs => fs.GroupSeason)
                .Where(fsm => fsm.Season == seasonYear)
                .AsNoTracking()
                .ToListAsync();

            var dtos = metrics.Select(fsm => new FranchiseSeasonMetricsDto()
            {
                Conference = fsm.FranchiseSeason.GroupSeason?.Slug,
                ExplosiveRate = fsm.ExplosiveRate,
                FgPctShrunk = fsm.FgPctShrunk,
                FieldPosDiff = fsm.FieldPosDiff,
                FranchiseName = fsm.FranchiseSeason.Franchise.DisplayNameShort,
                FranchiseSlug = fsm.FranchiseSeason.Franchise.Slug,
                GamesPlayed = fsm.GamesPlayed,
                NetPunt = fsm.NetPunt,
                OppExplosiveRate = fsm.OppExplosiveRate,
                OppPointsPerDrive = fsm.OppPointsPerDrive,
                OppRzTdRate = fsm.OppRzTdRate,
                OppSuccessRate = fsm.OppSuccessRate,
                OppThirdFourthRate = fsm.OppThirdFourthRate,
                OppYpp = fsm.OppYpp,
                PenaltyYardsPerPlay = fsm.PenaltyYardsPerPlay,
                PointsPerDrive = fsm.PointsPerDrive,
                RzScoreRate = fsm.RzScoreRate,
                RzTdRate = fsm.RzTdRate,
                SeasonYear = seasonYear,
                SuccessRate = fsm.SuccessRate,
                ThirdFourthRate = fsm.ThirdFourthRate,
                TimePossRatio = fsm.TimePossRatio,
                TurnoverMarginPerDrive = fsm.TurnoverMarginPerDrive,
                Ypp = fsm.Ypp
            }).ToList();

            return dtos;
        }

        public async Task<FranchiseSeasonMetricsDto> GetFranchiseSeasonMetricsByFranchiseSeasonId(Guid franchiseSeasonId)
        {
            var metric = await _dataContext.FranchiseSeasonMetrics
                .Include(fsm => fsm.FranchiseSeason)
                    .ThenInclude(fs => fs.Franchise)
                .Include(fsm => fsm.FranchiseSeason)
                    .ThenInclude(fs => fs.GroupSeason)
                .AsNoTracking()
                .FirstOrDefaultAsync(fsm => fsm.FranchiseSeasonId == franchiseSeasonId);

            if (metric == null)
            {
                _logger.LogWarning("No FranchiseSeasonMetric found for FranchiseSeasonId: {FranchiseSeasonId}", franchiseSeasonId);
                throw new InvalidOperationException($"FranchiseSeasonMetric not found for FranchiseSeasonId: {franchiseSeasonId}");
            }

            var dto = new FranchiseSeasonMetricsDto()
            {
                Conference = metric.FranchiseSeason.GroupSeason?.Slug,
                ExplosiveRate = metric.ExplosiveRate,
                FgPctShrunk = metric.FgPctShrunk,
                FieldPosDiff = metric.FieldPosDiff,
                FranchiseName = metric.FranchiseSeason.Franchise.DisplayNameShort,
                FranchiseSlug = metric.FranchiseSeason.Franchise.Slug,
                GamesPlayed = metric.GamesPlayed,
                NetPunt = metric.NetPunt,
                OppExplosiveRate = metric.OppExplosiveRate,
                OppPointsPerDrive = metric.OppPointsPerDrive,
                OppRzTdRate = metric.OppRzTdRate,
                OppSuccessRate = metric.OppSuccessRate,
                OppThirdFourthRate = metric.OppThirdFourthRate,
                OppYpp = metric.OppYpp,
                PenaltyYardsPerPlay = metric.PenaltyYardsPerPlay,
                PointsPerDrive = metric.PointsPerDrive,
                RzScoreRate = metric.RzScoreRate,
                RzTdRate = metric.RzTdRate,
                SeasonYear = metric.Season,
                SuccessRate = metric.SuccessRate,
                ThirdFourthRate = metric.ThirdFourthRate,
                TimePossRatio = metric.TimePossRatio,
                TurnoverMarginPerDrive = metric.TurnoverMarginPerDrive,
                Ypp = metric.Ypp
            };

            return dto;
        }

        public async Task GenerateFranchiseSeasonMetrics(GenerateFranchiseSeasonMetricsCommand command)
        {
            var fbsGroupIds = await _groupSeasonsService.GetFbsGroupSeasonIds(command.SeasonYear);

            var franchiseSeasons = await _dataContext.FranchiseSeasons
                .Include(fs => fs.Franchise)
                .Where(fs =>
                    fs.GroupSeasonId != null &&
                    fs.SeasonYear == command.SeasonYear &&
                    fs.Franchise.Sport == command.Sport &&
                    fbsGroupIds.Contains(fs.GroupSeasonId!.Value))
                .AsNoTracking()
                .ToListAsync();

            foreach (var fs in franchiseSeasons)
            {
                _backgroundJobProvider.Enqueue<IFranchiseSeasonMetricsService>(
                    x => x.GenerateFranchiseSeasonMetrics(fs.Id, command.SeasonYear));
            }
        }

        public async Task GenerateFranchiseSeasonMetrics(Guid franchiseSeasonId, int seasonYear)
        {
            // Saved for posterity in case I want to calculate home and away metrics

            //var competitions = await _dataContext.Contests
            //    .Where(c => c.AwayTeamFranchiseSeasonId == franchiseSeasonId ||
            //                c.HomeTeamFranchiseSeasonId == franchiseSeasonId)
            //    .SelectMany(c => c.Competitions.Select(comp => new
            //    {
            //        CompetitionId = comp.Id,
            //        IsHome = c.HomeTeamFranchiseSeasonId == franchiseSeasonId,
            //        IsAway = c.AwayTeamFranchiseSeasonId == franchiseSeasonId
            //    }))
            //    .ToListAsync();

            var competitionIds = await _dataContext.Contests
                .Where(x => x.AwayTeamFranchiseSeasonId == franchiseSeasonId ||
                            x.HomeTeamFranchiseSeasonId == franchiseSeasonId)
                .SelectMany(x => x.Competitions.Select(c => c.Id))
                .ToListAsync();

            var metrics = await _dataContext.CompetitionMetrics
                .Where(cm => competitionIds.Contains(cm.CompetitionId) && cm.FranchiseSeasonId == franchiseSeasonId)
                .ToListAsync();

            if (!metrics.Any())
            {
                _logger.LogInformation("No competition metrics found for FranchiseSeasonId {FranchiseSeasonId} in season {SeasonYear}. Skipping.", franchiseSeasonId, seasonYear);
                return;
            }

            // calculate the averages
            var fsMetric = ComputeFranchiseSeasonMetric(metrics);
            fsMetric.FranchiseSeasonId = franchiseSeasonId;
            fsMetric.Season = seasonYear;

            var existingMetric = await _dataContext.FranchiseSeasonMetrics
                .FirstOrDefaultAsync(fsm => fsm.FranchiseSeasonId == franchiseSeasonId && fsm.Season == seasonYear);
            if (existingMetric != null)
            {
                _dataContext.FranchiseSeasonMetrics.Remove(existingMetric);
            }

            await _dataContext.FranchiseSeasonMetrics.AddAsync(fsMetric);
            await _dataContext.SaveChangesAsync();
        }

        public static FranchiseSeasonMetric ComputeFranchiseSeasonMetric(List<CompetitionMetric> metrics)
        {
            if (metrics == null || metrics.Count == 0)
                throw new ArgumentException("Cannot compute averages from an empty list.", nameof(metrics));

            decimal? SafeAvg(Func<CompetitionMetric, decimal?> selector) =>
                metrics.Select(selector).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty().Average();

            return new FranchiseSeasonMetric
            {
                GamesPlayed = metrics.Count,

                // Offense
                Ypp = metrics.Average(m => m.Ypp),
                SuccessRate = metrics.Average(m => m.SuccessRate),
                ExplosiveRate = metrics.Average(m => m.ExplosiveRate),
                PointsPerDrive = metrics.Average(m => m.PointsPerDrive),
                ThirdFourthRate = metrics.Average(m => m.ThirdFourthRate),
                RzTdRate = SafeAvg(m => m.RzTdRate),
                RzScoreRate = SafeAvg(m => m.RzScoreRate),
                TimePossRatio = metrics.Average(m => m.TimePossRatio),

                // Defense
                OppYpp = metrics.Average(m => m.OppYpp),
                OppSuccessRate = metrics.Average(m => m.OppSuccessRate),
                OppExplosiveRate = metrics.Average(m => m.OppExplosiveRate),
                OppPointsPerDrive = metrics.Average(m => m.OppPointsPerDrive),
                OppThirdFourthRate = metrics.Average(m => m.OppThirdFourthRate),
                OppRzTdRate = SafeAvg(m => m.OppRzTdRate),
                OppScoreTdRate = SafeAvg(m => m.OppScoreTdRate),

                // ST / Discipline
                NetPunt = metrics.Average(m => m.NetPunt),
                FgPctShrunk = metrics.Average(m => m.FgPctShrunk),
                FieldPosDiff = metrics.Average(m => m.FieldPosDiff),
                TurnoverMarginPerDrive = metrics.Average(m => m.TurnoverMarginPerDrive),
                PenaltyYardsPerPlay = metrics.Average(m => m.PenaltyYardsPerPlay),

                ComputedUtc = DateTime.UtcNow
            };
        }

    }

    public class GenerateFranchiseSeasonMetricsCommand
    {
        public Sport Sport { get; set; }

        public int SeasonYear { get; set; }

        public Guid CorrelationId { get; set; }
    }
}
