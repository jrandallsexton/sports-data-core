using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.FranchiseSeasons
{
    public interface IFranchiseSeasonMetricsService
    {
        Task GenerateFranchiseSeasonMetrics(GenerateFranchiseSeasonMetricsCommand command);
        Task GenerateFranchiseSeasonMetrics(Guid franchiseSeasonId, int seasonYear);
    }

    public class FranchiseSeasonMetricsService : IFranchiseSeasonMetricsService
    {
        private readonly ILogger<FranchiseSeasonMetricsService> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FranchiseSeasonMetricsService(
            ILogger<FranchiseSeasonMetricsService> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task GenerateFranchiseSeasonMetrics(GenerateFranchiseSeasonMetricsCommand command)
        {
            var groupSeasons = await _dataContext.GroupSeasons
                .Where(gs => gs.SeasonYear == command.SeasonYear)
                .AsNoTracking()
                .ToListAsync();

            // Get all FBS roots (may be duplicates due to ESPN data)
            var fbsRoots = groupSeasons
                .Where(gs => gs.Slug == "fbs-i-a")
                .ToList();

            if (!fbsRoots.Any())
                throw new InvalidOperationException("FBS group root(s) not found.");

            // Collect all descendant group IDs from all FBS roots
            var fbsGroupIds = new HashSet<Guid>();
            foreach (var root in fbsRoots)
            {
                var descendants = GetAllDescendantGroupIds(root.Id, groupSeasons);
                foreach (var id in descendants)
                    fbsGroupIds.Add(id);
            }

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

        private static HashSet<Guid> GetAllDescendantGroupIds(Guid rootId, List<GroupSeason> allGroups)
        {
            var result = new HashSet<Guid> { rootId };
            var queue = new Queue<Guid>();
            queue.Enqueue(rootId);

            while (queue.Count > 0)
            {
                var currentId = queue.Dequeue();
                var children = allGroups
                    .Where(g => g.ParentId == currentId)
                    .Select(g => g.Id);

                foreach (var childId in children)
                {
                    if (result.Add(childId))
                        queue.Enqueue(childId);
                }
            }

            return result;
        }
    }

    public class GenerateFranchiseSeasonMetricsCommand
    {
        public Sport Sport { get; set; }

        public int SeasonYear { get; set; }

        public Guid CorrelationId { get; set; }
    }
}
