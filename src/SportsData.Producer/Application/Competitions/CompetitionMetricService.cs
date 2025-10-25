using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.Competitions
{
    public interface ICompetitionMetricService
    {
        Task CalculateCompetitionMetrics(Guid competitionId);
    }

    public class CompetitionMetricService : ICompetitionMetricService
    {
        private readonly ILogger<CompetitionMetricService> _logger;
        private readonly TeamSportDataContext _dataContext;

        public CompetitionMetricService(
            ILogger<CompetitionMetricService> logger,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }


        public async Task CalculateCompetitionMetrics(Guid competitionId)
        {
            // delete existing metrics for this competition
            var existingMetrics = _dataContext.CompetitionMetrics
                .Where(m => m.CompetitionId == competitionId);
            _dataContext.CompetitionMetrics.RemoveRange(existingMetrics);
            await _dataContext.SaveChangesAsync();

            var competition = await _dataContext.Competitions
                .AsNoTracking()
                .Include(x => x.Contest)
                .ThenInclude(c => c.AwayTeamFranchiseSeason)
                .Include(x => x.Contest)
                .ThenInclude(c => c.HomeTeamFranchiseSeason)
                .Include(x => x.Drives.OrderBy(y => y.SequenceNumber))
                .ThenInclude(d => d.Plays.OrderBy(p => p.SequenceNumber))
                .Include(x => x.Plays.OrderBy(p => p.SequenceNumber))
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition == null)
            {
                _logger.LogError("Competition not found");
                return;
            }

            var awayFranchiseSeasonId = competition.Contest.AwayTeamFranchiseSeasonId;
            var homeFranchiseSeasonId = competition.Contest.HomeTeamFranchiseSeasonId;

            var (awayMetric, homeMetric) = CalculateMetrics(
                competitionId,
                competition.Plays.ToList(),
                competition.Drives.ToList(),
                awayFranchiseSeasonId,
                homeFranchiseSeasonId);

            await _dataContext.CompetitionMetrics.AddAsync(awayMetric);
            await _dataContext.CompetitionMetrics.AddAsync(homeMetric);
            await _dataContext.SaveChangesAsync();
        }

        private (CompetitionMetric, CompetitionMetric) CalculateMetrics(
            Guid competitionId,
            List<CompetitionPlay> plays,
            List<CompetitionDrive> drives,
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId)
        {
            var awayMetric = new CompetitionMetric
            {
                CompetitionId = competitionId,
                FranchiseSeasonId = awayFranchiseSeasonId,
                Season = DateTime.UtcNow.Year, // TODO: Get from competition/contest
                Ypp = CalculateYpp(awayFranchiseSeasonId, plays),
                SuccessRate = CalculateSuccessRate(awayFranchiseSeasonId, plays),
                ExplosiveRate = CalculateExplosiveRate(awayFranchiseSeasonId, plays),
                ThirdFourthRate = CalculateThirdFourthConversionRate(awayFranchiseSeasonId, plays),
                PointsPerDrive = CalculatePointsPerDrive(awayFranchiseSeasonId, plays, homeFranchiseSeasonId, awayFranchiseSeasonId),
                RzTdRate = CalculateRedZoneTdRate(awayFranchiseSeasonId, plays),
                RzScoreRate = CalculateRedZoneScoringRate(awayFranchiseSeasonId, plays),
                TimePossRatio = CalculateTimeOfPossessionRatio(awayFranchiseSeasonId, homeFranchiseSeasonId, plays),
                // Opponent metrics (from home team's perspective)
                OppYpp = CalculateYpp(homeFranchiseSeasonId, plays),
                OppSuccessRate = CalculateSuccessRate(homeFranchiseSeasonId, plays),
                OppExplosiveRate = CalculateExplosiveRate(homeFranchiseSeasonId, plays),
                OppPointsPerDrive = CalculatePointsPerDrive(homeFranchiseSeasonId, plays, homeFranchiseSeasonId, awayFranchiseSeasonId),
                OppThirdFourthRate = CalculateThirdFourthConversionRate(homeFranchiseSeasonId, plays),
                OppRzTdRate = CalculateRedZoneTdRate(homeFranchiseSeasonId, plays),
                OppScoreTdRate = CalculateRedZoneScoringRate(homeFranchiseSeasonId, plays),
                // Special teams / Discipline (TODO)
                NetPunt = 0m,
                FgPctShrunk = CalculateFgPctShrunk(awayFranchiseSeasonId, plays),
                FieldPosDiff = CalculateFieldPositionDiff(awayFranchiseSeasonId, drives),
                TurnoverMarginPerDrive = CalculateTurnoverMarginPerDrive(awayFranchiseSeasonId, plays, drives),
                PenaltyYardsPerPlay = CalculatePenaltyYardsPerPlay(awayFranchiseSeasonId, plays),
                // Bookkeeping
                ComputedUtc = DateTime.UtcNow,
                InputsHash = null
            };

            var homeMetric = new CompetitionMetric
            {
                CompetitionId = competitionId,
                FranchiseSeasonId = homeFranchiseSeasonId,
                Season = DateTime.UtcNow.Year, // TODO: Get from competition/contest
                Ypp = CalculateYpp(homeFranchiseSeasonId, plays),
                SuccessRate = CalculateSuccessRate(homeFranchiseSeasonId, plays),
                ExplosiveRate = CalculateExplosiveRate(homeFranchiseSeasonId, plays),
                ThirdFourthRate = CalculateThirdFourthConversionRate(homeFranchiseSeasonId, plays),
                PointsPerDrive = CalculatePointsPerDrive(homeFranchiseSeasonId, plays, homeFranchiseSeasonId, awayFranchiseSeasonId),
                RzTdRate = CalculateRedZoneTdRate(homeFranchiseSeasonId, plays),
                RzScoreRate = CalculateRedZoneScoringRate(homeFranchiseSeasonId, plays),
                TimePossRatio = CalculateTimeOfPossessionRatio(homeFranchiseSeasonId, awayFranchiseSeasonId, plays),
                // Opponent metrics (from away team's perspective)
                OppYpp = CalculateYpp(awayFranchiseSeasonId, plays),
                OppSuccessRate = CalculateSuccessRate(awayFranchiseSeasonId, plays),
                OppExplosiveRate = CalculateExplosiveRate(awayFranchiseSeasonId, plays),
                OppPointsPerDrive = CalculatePointsPerDrive(awayFranchiseSeasonId, plays, homeFranchiseSeasonId, awayFranchiseSeasonId),
                OppThirdFourthRate = CalculateThirdFourthConversionRate(awayFranchiseSeasonId, plays),
                OppRzTdRate = CalculateRedZoneTdRate(awayFranchiseSeasonId, plays),
                OppScoreTdRate = CalculateRedZoneScoringRate(awayFranchiseSeasonId, plays),
                // Special teams / Discipline (TODO)
                NetPunt = 0m,
                FgPctShrunk = CalculateFgPctShrunk(homeFranchiseSeasonId, plays),
                FieldPosDiff = CalculateFieldPositionDiff(homeFranchiseSeasonId, drives),
                TurnoverMarginPerDrive = CalculateTurnoverMarginPerDrive(homeFranchiseSeasonId, plays, drives),
                PenaltyYardsPerPlay = CalculatePenaltyYardsPerPlay(homeFranchiseSeasonId, plays),
                // Bookkeeping
                ComputedUtc = DateTime.UtcNow,
                InputsHash = null
            };

            return (awayMetric, homeMetric);
        }

        private static decimal CalculateTimeOfPossessionRatio(
            Guid franchiseSeasonId,
            Guid opponentFranchiseSeasonId,
            IReadOnlyCollection<CompetitionPlay> plays)
        {
            double GetTeamSeconds(Guid fsId)
            {
                return plays
                    .Where(p => p.DriveId != null && p.StartFranchiseSeasonId == fsId)
                    .GroupBy(p => p.DriveId)
                    .Sum(drive =>
                    {
                        var ordered = drive.OrderBy(p => p.SequenceNumber).ToList();
                        var first = ordered.FirstOrDefault();
                        var last = ordered.LastOrDefault();

                        if (first == null || last == null) return 0;

                        // ESPN clocks count *down* from 900 → normalize to seconds remaining in game
                        double firstTime = GameClockInSeconds(first);
                        double lastTime = GameClockInSeconds(last);

                        return Math.Max(0, firstTime - lastTime);
                    });
            }

            double GameClockInSeconds(CompetitionPlay play)
            {
                double clock = play.ClockValue;
                int period = play.PeriodNumber;

                // 4 quarters, each 900 seconds → time remaining = total seconds remaining in game
                int secondsRemaining = (4 - period) * 900 + (int)Math.Round(clock);
                return secondsRemaining;
            }

            var teamSec = GetTeamSeconds(franchiseSeasonId);
            var oppSec = GetTeamSeconds(opponentFranchiseSeasonId);
            var total = teamSec + oppSec;

            if (total == 0) return 0m;
            return Math.Round((decimal)(teamSec / total), 4);
        }

        private static decimal CalculateFgPctShrunk(
            Guid franchiseSeasonId,
            IReadOnlyCollection<CompetitionPlay> plays,
            int maxDistance = 45)
        {
            var fgAttempts = plays
                .Where(p =>
                    p.StartFranchiseSeasonId == franchiseSeasonId &&
                    (p.Type == PlayType.FieldGoalGood || p.Type == PlayType.FieldGoalMissed) &&
                    p.StatYardage > 0 &&
                    p.StatYardage <= maxDistance)
                .ToList();

            if (fgAttempts.Count == 0)
                return 0m;

            var madeFgs = fgAttempts.Count(p => p.Type == PlayType.FieldGoalGood);

            return Math.Round((decimal)madeFgs / fgAttempts.Count, 4);
        }


        private static decimal CalculateFieldPositionDiff(
            Guid teamId,
            IReadOnlyCollection<CompetitionDrive> drives)
        {
            var myDrives = drives.Where(d => d.StartFranchiseSeasonId == teamId && d.StartYardLine.HasValue).ToList();
            var oppDrives = drives.Where(d => d.StartFranchiseSeasonId != teamId && d.StartYardLine.HasValue).ToList();

            if (!myDrives.Any() || !oppDrives.Any())
                return 0m;

            var myAvg = (decimal)myDrives.Average(d => d.StartYardLine!.Value);
            var oppAvg = (decimal)oppDrives.Average(d => d.StartYardLine!.Value);

            return Math.Round(myAvg - oppAvg, 2);
        }

        private static decimal CalculateTurnoverMarginPerDrive(
            Guid teamId,
            IReadOnlyCollection<CompetitionPlay> plays,
            IReadOnlyCollection<CompetitionDrive> drives)
        {
            if (!plays.Any() || !drives.Any())
                return 0m;

            // Plays where *this* team lost possession
            var turnoversLost = plays.Count(p =>
                p.StartFranchiseSeasonId == teamId &&
                (p.Type == PlayType.FumbleLost ||
                 p.Type == PlayType.PassInterceptionReturn ||
                 p.Type == PlayType.InterceptionReturnTouchdown));

            // Plays where *opponent* lost possession = turnovers gained
            var turnoversGained = plays.Count(p =>
                p.StartFranchiseSeasonId != teamId &&
                (p.Type == PlayType.FumbleLost ||
                 p.Type == PlayType.PassInterceptionReturn ||
                 p.Type == PlayType.InterceptionReturnTouchdown));

            // Count of drives *started* by this team
            var offensiveDrives = drives.Count(d => d.StartFranchiseSeasonId == teamId);

            if (offensiveDrives == 0)
                return 0m;

            var margin = turnoversGained - turnoversLost;
            return Math.Round((decimal)margin / offensiveDrives, 4);
        }


        private decimal CalculatePenaltyYardsPerPlay(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            var penalties = plays
                .Where(p => p.Type == PlayType.Penalty && p.StartFranchiseSeasonId == franchiseSeasonId)
                .ToList();

            if (penalties.Count == 0) return 0m;

            var offensiveSnaps = plays
                .Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId))
                .Count();

            if (offensiveSnaps == 0) return 0m;

            var totalPenaltyYards = penalties.Sum(p => Math.Abs(p.StatYardage));

            return (decimal)totalPenaltyYards / offensiveSnaps;
        }


        private decimal CalculateYpp(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            var snaps = plays.Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId)).ToList();
            if (snaps.Count == 0) return 0m;

            var yards = snaps.Sum(Yardage);           // int total
            return (decimal)yards / snaps.Count;      // force decimal division
        }

        // Success Rate (0..1) using your 7/4/2 heuristic
        // 1st: >=7 yards OR first down by yardage
        // 2nd: >=4 yards OR first down by yardage
        // 3rd/4th: first down by yardage OR >=2 yards
        private decimal CalculateSuccessRate(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            var snaps = plays.Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId)).ToList();
            if (snaps.Count == 0) return 0m;

            int successes = 0;

            foreach (var p in snaps)
            {
                var down = p.StartDown ?? 0;
                var yds = Yardage(p);

                bool success = down switch
                {
                    1 => yds >= 7 || AchievedFirstDownByYardage(p),
                    2 => yds >= 4 || AchievedFirstDownByYardage(p),
                    3 => AchievedFirstDownByYardage(p) || yds >= 2,
                    4 => AchievedFirstDownByYardage(p) || yds >= 2,
                    _ => false
                };

                if (success) successes++;
            }

            return (decimal)successes / snaps.Count;
        }

        // Explosive Rate (0..1): fraction of offensive scrimmage snaps gaining >= threshold yards.
        // Default threshold is 20 (common definition).
        private decimal CalculateExplosiveRate(
            Guid franchiseSeasonId,
            List<CompetitionPlay> plays,
            int thresholdYards = 20)
        {
            var snaps = plays.Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId)).ToList();
            if (snaps.Count == 0) return 0m;

            var explosive = snaps.Count(p => Yardage(p) >= thresholdYards);
            return (decimal)explosive / snaps.Count;
        }

        // Third/Fourth Conversion Rate (0..1)
        // attempts: offensive scrimmage snaps on 3rd or 4th down
        // conversions: first down gained by yardage on those snaps
        private decimal CalculateThirdFourthConversionRate(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            var snaps = plays.Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId)
                                         && (p.StartDown == 3 || p.StartDown == 4))
                .ToList();

            if (snaps.Count == 0) return 0m;

            var conversions = snaps.Count(p => AchievedFirstDownByYardage(p));
            return (decimal)conversions / snaps.Count;
        }

        // PPD from plays only (ordered). We infer drives by contiguous offensive scrimmage snaps by the same offense.
        // homeFsId / awayFsId are used to select the team's score on each play.
        // PPD from plays only (ordered). We infer drives by contiguous offensive scrimmage snaps
        // by the same offense. homeFsId/awayFsId are used to read the correct scoreboard.
        private decimal CalculatePointsPerDrive(
            Guid franchiseSeasonId,
            List<CompetitionPlay> plays,
            Guid homeFsId,
            Guid awayFsId)
        {
            var drives = plays
                .Where(p => p.DriveId != Guid.Empty && p.StartFranchiseSeasonId == franchiseSeasonId)
                .GroupBy(p => p.DriveId)
                .ToList();

            if (drives.Count == 0) return 0m;

            var totalPoints = 0;

            foreach (var drive in drives)
            {
                // Use last play of the drive to infer final score for that possession
                var lastPlay = drive.OrderBy(p => p.SequenceNumber).LastOrDefault();
                if (lastPlay == null) continue;

                var offensePoints = franchiseSeasonId == homeFsId
                    ? lastPlay.HomeScore
                    : lastPlay.AwayScore;

                var previousPlay = drive.OrderBy(p => p.SequenceNumber).SkipLast(1).LastOrDefault();
                var previousScore = previousPlay == null
                    ? 0
                    : (franchiseSeasonId == homeFsId ? previousPlay.HomeScore : previousPlay.AwayScore);

                var drivePoints = offensePoints - previousScore;
                if (drivePoints >= 0) totalPoints += drivePoints;
            }

            return (decimal)totalPoints / drives.Count;
        }


        // Red Zone TD Rate (null if no trips): TD-trips / trips
        private decimal? CalculateRedZoneTdRate(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            int trips = 0;
            int tdTrips = 0;

            bool inTrip = false;
            bool tripHadTd = false;

            for (int i = 0; i < plays.Count; i++)
            {
                var p = plays[i];

                // detect trip start: this offense, scrimmage snap, and at/below the 20
                if (!inTrip
                    && IsOffensiveScrimmageSnap(p, franchiseSeasonId)
                    && (p.StartYardsToEndzone.HasValue && p.StartYardsToEndzone.Value <= 20))
                {
                    inTrip = true;
                    tripHadTd = false;
                    trips++;
                }

                if (!inTrip) continue;

                // inside a trip, watch for THIS offense scoring a TD (rush/pass)
                if (p.StartFranchiseSeasonId.HasValue
                    && p.StartFranchiseSeasonId.Value == franchiseSeasonId)
                {
                    if (p.Type == PlayType.RushingTouchdown || p.Type == PlayType.PassingTouchdown)
                    {
                        tripHadTd = true;
                    }
                }

                // trip ends when the OTHER offense takes a scrimmage snap that stands
                if (p.StartFranchiseSeasonId.HasValue
                    && p.StartFranchiseSeasonId.Value != franchiseSeasonId
                    && p.StartDown is >= 1 and <= 4
                    && IsOffensiveScrimmageType(p.Type)
                    && !(!string.IsNullOrEmpty(p.Text) && p.Text.Contains("NO PLAY", StringComparison.OrdinalIgnoreCase)))
                {
                    if (tripHadTd) tdTrips++;
                    inTrip = false;
                }
            }

            // if a trip is still open at EOF, close it
            if (inTrip && tripHadTd) tdTrips++;

            if (trips == 0) return null;
            return (decimal)tdTrips / trips;
        }

        // Red Zone Scoring Rate (null if no trips): scoring-trips / trips
        // A trip starts on this offense's first scrimmage snap with StartYardsToEndzone <= 20
        // A scoring trip is one where, before the trip ends, THIS offense records:
        //   - RushingTouchdown or PassingTouchdown, OR
        //   - FieldGoalGood
        private decimal? CalculateRedZoneScoringRate(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            int trips = 0;
            int scoringTrips = 0;

            bool inTrip = false;
            bool tripScored = false;

            for (int i = 0; i < plays.Count; i++)
            {
                var p = plays[i];

                // trip starts on first offensive scrimmage snap at/below the 20
                if (!inTrip
                    && IsOffensiveScrimmageSnap(p, franchiseSeasonId)
                    && p.StartYardsToEndzone.HasValue
                    && p.StartYardsToEndzone.Value <= 20)
                {
                    inTrip = true;
                    tripScored = false;
                    trips++;
                }

                if (!inTrip) continue;

                // scoring for THIS offense during the trip
                if (p.StartFranchiseSeasonId.HasValue
                    && p.StartFranchiseSeasonId.Value == franchiseSeasonId)
                {
                    if (p.Type == PlayType.RushingTouchdown || p.Type == PlayType.PassingTouchdown)
                        tripScored = true;

                    if (p.Type == PlayType.FieldGoalGood)
                        tripScored = true;
                }

                // trip ends when the OTHER offense takes a scrimmage snap that stands
                if (p.StartFranchiseSeasonId.HasValue
                    && p.StartFranchiseSeasonId.Value != franchiseSeasonId
                    && p.StartDown is >= 1 and <= 4
                    && IsOffensiveScrimmageType(p.Type)
                    && !(p.Text?.Contains("NO PLAY", StringComparison.OrdinalIgnoreCase) == true))
                {
                    if (tripScored) scoringTrips++;
                    inTrip = false;
                }
            }

            // close an open trip at EOF
            if (inTrip && tripScored) scoringTrips++;

            if (trips == 0) return (decimal?)null;
            return (decimal)scoringTrips / trips;
        }


        /* ================= HELPERS ================ */
        private static int Yardage(CompetitionPlay p) => p.StatYardage;

        // first down purely by distance-to-gain (no FirstDown flag in your data)
        private static bool AchievedFirstDownByYardage(CompetitionPlay p)
            => p.StartDistance.HasValue && Yardage(p) >= p.StartDistance.Value;

        // which team is on offense at the snap? (null means unknown → not this offense)
        private static bool IsOffense(CompetitionPlay p, Guid franchiseSeasonId)
            => p.StartFranchiseSeasonId.HasValue
               && p.StartFranchiseSeasonId.Value == franchiseSeasonId;

        // helper: which play types count as *offensive scrimmage snaps* for team metrics?
        private static bool IsOffensiveScrimmageType(PlayType t)
        {
            // Include normal snaps from scrimmage:
            // - Rush / Pass (comp & inc) / Sack
            // - TD variants that ESPN sometimes logs as distinct types
            // - Safety (comes from a scrimmage snap)
            return t == PlayType.Rush
                   || t == PlayType.RushingTouchdown
                   || t == PlayType.PassReception
                   || t == PlayType.PassingTouchdown
                   || t == PlayType.PassIncompletion
                   || t == PlayType.Sack
                   || t == PlayType.Safety;
        }

        // full filter for "counts toward offense's snaps"
        private static bool IsOffensiveScrimmageSnap(CompetitionPlay p, Guid franchiseSeasonId)
        {
            if (!IsOffense(p, franchiseSeasonId)) return false;

            // must be an actual down (1–4) at the snap
            if (!p.StartDown.HasValue || p.StartDown < 1 || p.StartDown > 4) return false;

            if (!IsOffensiveScrimmageType(p.Type)) return false;

            // exclude accepted penalties that void the snap
            if (!string.IsNullOrEmpty(p.Text) &&
                p.Text.Contains("NO PLAY", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
