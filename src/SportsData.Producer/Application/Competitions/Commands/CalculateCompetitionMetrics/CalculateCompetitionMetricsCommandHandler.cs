using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;

using System.Security.Cryptography;
using System.Text;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;

public interface ICalculateCompetitionMetricsCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(CalculateCompetitionMetricsCommand command, CancellationToken cancellationToken = default);
}

public class CalculateCompetitionMetricsCommandHandler : ICalculateCompetitionMetricsCommandHandler
{
    private readonly ILogger<CalculateCompetitionMetricsCommandHandler> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CalculateCompetitionMetricsCommandHandler(
        ILogger<CalculateCompetitionMetricsCommandHandler> logger,
        FootballDataContext dataContext,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        CalculateCompetitionMetricsCommand command,
        CancellationToken cancellationToken = default)
    {
        // Mark existing metrics for deletion; the delete and the insert
        // below commit in ONE SaveChanges (EF orders same-table deletes
        // before inserts), so a crash mid-recompute cannot leave the
        // competition with no rows (audit doc: recompute contract).
        var existingMetrics = await _dataContext.CompetitionMetrics
            .Where(m => m.CompetitionId == command.CompetitionId)
            .ToListAsync(cancellationToken);

        _dataContext.CompetitionMetrics.RemoveRange(existingMetrics);

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
            .FirstOrDefaultAsync(x => x.Id == command.CompetitionId, cancellationToken);

        if (competition == null)
        {
            _logger.LogError("Competition not found for ID {CompetitionId}", command.CompetitionId);
            return new Failure<Guid>(
                command.CompetitionId,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.CompetitionId), "Competition not found")]
            );
        }

        var awayFranchiseSeasonId = competition.Contest.AwayTeamFranchiseSeasonId;
        var homeFranchiseSeasonId = competition.Contest.HomeTeamFranchiseSeasonId;

        var orderedPlays = OrderPlays(competition.Plays);
        var drives = competition.Drives.ToList();

        var (awayMetric, homeMetric) = CalculateMetrics(
            competition.Contest.SeasonYear,
            command.CompetitionId,
            orderedPlays,
            drives,
            awayFranchiseSeasonId,
            homeFranchiseSeasonId);

        // Bookkeeping stamps (audit doc: recompute contract). Both rows
        // share one hash: recompute may skip a competition whose stored
        // (InputsHash, FormulaVersion) both match.
        var computedUtc = _dateTimeProvider.UtcNow();
        var inputsHash = ComputeInputsHash(orderedPlays, drives);
        foreach (var metric in new[] { awayMetric, homeMetric })
        {
            metric.ComputedUtc = computedUtc;
            metric.InputsHash = inputsHash;
            metric.FormulaVersion = MetricFormula.Version;
        }

        await _dataContext.CompetitionMetrics.AddAsync(awayMetric, cancellationToken);
        await _dataContext.CompetitionMetrics.AddAsync(homeMetric, cancellationToken);

        // TODO: Raise integration event?
        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<Guid>(command.CompetitionId);
    }

    private (CompetitionMetric, CompetitionMetric) CalculateMetrics(
        int seasonYear,
        Guid competitionId,
        List<FootballCompetitionPlay> plays,
        List<CompetitionDrive> drives,
        Guid awayFranchiseSeasonId,
        Guid homeFranchiseSeasonId)
    {
        var awayMetric = new CompetitionMetric
        {
            CompetitionId = competitionId,
            FranchiseSeasonId = awayFranchiseSeasonId,
            Season = seasonYear,
            Ypp = CalculateYpp(awayFranchiseSeasonId, plays),
            SuccessRate = CalculateSuccessRate(awayFranchiseSeasonId, plays),
            ExplosiveRate = CalculateExplosiveRate(awayFranchiseSeasonId, plays),
            ThirdFourthRate = CalculateThirdFourthConversionRate(awayFranchiseSeasonId, plays),
            PointsPerDrive = CalculatePointsPerDrive(awayFranchiseSeasonId, plays, homeFranchiseSeasonId),
            RzTdRate = CalculateRedZoneTdRate(awayFranchiseSeasonId, plays),
            RzScoreRate = CalculateRedZoneScoringRate(awayFranchiseSeasonId, plays),
            TimePossRatio = CalculateTimeOfPossessionRatio(awayFranchiseSeasonId, homeFranchiseSeasonId, plays),
            // Opponent metrics (from home team's perspective)
            OppYpp = CalculateYpp(homeFranchiseSeasonId, plays),
            OppSuccessRate = CalculateSuccessRate(homeFranchiseSeasonId, plays),
            OppExplosiveRate = CalculateExplosiveRate(homeFranchiseSeasonId, plays),
            OppPointsPerDrive = CalculatePointsPerDrive(homeFranchiseSeasonId, plays, homeFranchiseSeasonId),
            OppThirdFourthRate = CalculateThirdFourthConversionRate(homeFranchiseSeasonId, plays),
            OppRzTdRate = CalculateRedZoneTdRate(homeFranchiseSeasonId, plays),
            OppScoreTdRate = CalculateRedZoneScoringRate(homeFranchiseSeasonId, plays),
            // Special teams / Discipline
            // AUDIT M4: no punting stats sourced yet — null, never a fake 0.
            NetPunt = null,
            FgPctShrunk = CalculateFgPctShrunk(awayFranchiseSeasonId, plays),
            FieldPosDiff = CalculateFieldPositionDiff(awayFranchiseSeasonId, drives),
            TurnoverMarginPerDrive = CalculateTurnoverMarginPerDrive(awayFranchiseSeasonId, plays, drives),
            // AUDIT H3: ESPN plays carry no penalized-team field; a metric
            // that cannot attribute its subject is not computed.
            PenaltyYardsPerPlay = null
        };

        var homeMetric = new CompetitionMetric
        {
            CompetitionId = competitionId,
            FranchiseSeasonId = homeFranchiseSeasonId,
            Season = seasonYear,
            Ypp = CalculateYpp(homeFranchiseSeasonId, plays),
            SuccessRate = CalculateSuccessRate(homeFranchiseSeasonId, plays),
            ExplosiveRate = CalculateExplosiveRate(homeFranchiseSeasonId, plays),
            ThirdFourthRate = CalculateThirdFourthConversionRate(homeFranchiseSeasonId, plays),
            PointsPerDrive = CalculatePointsPerDrive(homeFranchiseSeasonId, plays, homeFranchiseSeasonId),
            RzTdRate = CalculateRedZoneTdRate(homeFranchiseSeasonId, plays),
            RzScoreRate = CalculateRedZoneScoringRate(homeFranchiseSeasonId, plays),
            TimePossRatio = CalculateTimeOfPossessionRatio(homeFranchiseSeasonId, awayFranchiseSeasonId, plays),
            // Opponent metrics (from away team's perspective)
            OppYpp = CalculateYpp(awayFranchiseSeasonId, plays),
            OppSuccessRate = CalculateSuccessRate(awayFranchiseSeasonId, plays),
            OppExplosiveRate = CalculateExplosiveRate(awayFranchiseSeasonId, plays),
            OppPointsPerDrive = CalculatePointsPerDrive(awayFranchiseSeasonId, plays, homeFranchiseSeasonId),
            OppThirdFourthRate = CalculateThirdFourthConversionRate(awayFranchiseSeasonId, plays),
            OppRzTdRate = CalculateRedZoneTdRate(awayFranchiseSeasonId, plays),
            OppScoreTdRate = CalculateRedZoneScoringRate(awayFranchiseSeasonId, plays),
            // Special teams / Discipline
            // AUDIT M4: no punting stats sourced yet — null, never a fake 0.
            NetPunt = null,
            FgPctShrunk = CalculateFgPctShrunk(homeFranchiseSeasonId, plays),
            FieldPosDiff = CalculateFieldPositionDiff(homeFranchiseSeasonId, drives),
            TurnoverMarginPerDrive = CalculateTurnoverMarginPerDrive(homeFranchiseSeasonId, plays, drives),
            // AUDIT H3: ESPN plays carry no penalized-team field; a metric
            // that cannot attribute its subject is not computed.
            PenaltyYardsPerPlay = null
        };

        return (awayMetric, homeMetric);
    }

    private static decimal CalculateTimeOfPossessionRatio(
        Guid franchiseSeasonId,
        Guid opponentFranchiseSeasonId,
        IReadOnlyCollection<FootballCompetitionPlay> plays)
    {
        double GetTeamSeconds(Guid fsId)
        {
            // AUDIT M2: OT plays (period 5+) are EXCLUDED — OT possession
            // is not comparably clocked, and the OT clock overlaps the
            // Q4 0-900 range, so including it corrupts drive durations
            // (a Q4->OT drive was credited 930s pre-fix). This is a
            // REGULATION possession ratio by construction.
            return plays
                .Where(p => p.DriveId != null
                            && p.StartFranchiseSeasonId == fsId
                            && p.PeriodNumber <= 4)
                .GroupBy(p => p.DriveId)
                .Sum(drive =>
                {
                    var ordered = OrderPlays(drive);
                    var first = ordered.FirstOrDefault();
                    var last = ordered.LastOrDefault();

                    if (first == null || last == null) return 0;

                    // ESPN clocks count *down* from 900 → normalize to seconds remaining in game
                    double firstTime = GameClockInSeconds(first);
                    double lastTime = GameClockInSeconds(last);

                    return Math.Max(0, firstTime - lastTime);
                });
        }

        double GameClockInSeconds(FootballCompetitionPlay play)
        {
            double clock = play.ClockValue;
            int period = play.PeriodNumber;

            // Callers only pass periods 1-4 (see the M2 filter above);
            // Math.Max is defensive against that invariant breaking —
            // the unclamped form went NEGATIVE for period 5+.
            int secondsRemaining = Math.Max(0, 4 - period) * 900 + (int)Math.Round(clock);
            return secondsRemaining;
        }

        var teamSec = GetTeamSeconds(franchiseSeasonId);
        var oppSec = GetTeamSeconds(opponentFranchiseSeasonId);
        var total = teamSec + oppSec;

        if (total == 0) return 0m;
        return Math.Round((decimal)(teamSec / total), 4);
    }

    // AUDIT M3: null when a team has zero qualifying attempts — never 0%.
    // (The shrinkage prior implied by the name is deferred to a future
    // formula vintage; the audit doc is the honest record.)
    private static decimal? CalculateFgPctShrunk(
        Guid franchiseSeasonId,
        IReadOnlyCollection<FootballCompetitionPlay> plays,
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
            return null;

        var madeFgs = fgAttempts.Count(p => p.Type == PlayType.FieldGoalGood);

        return Math.Round((decimal)madeFgs / fgAttempts.Count, 4);
    }


    // AUDIT FIX (C2): the previous implementation differenced raw
    // StartYardLine values — a STADIUM-oriented coordinate where the
    // same physical spot reads 30 for one offense and 70 for the other.
    // The result mostly measured coordinate orientation (systematic
    // ±37 by venue), silently encoding home-field into per-game
    // features. StartYardsToEndzone is orientation-free: field position
    // = 100 − yards-to-endzone (own goal = 0, opponent goal = 100),
    // comparable across both offenses.
    private static decimal CalculateFieldPositionDiff(
        Guid teamId,
        IReadOnlyCollection<CompetitionDrive> drives)
    {
        var myStarts = drives
            .Where(d => d.StartFranchiseSeasonId == teamId && d.StartYardsToEndzone.HasValue)
            .Select(d => 100m - d.StartYardsToEndzone!.Value)
            .ToList();
        // HasValue guard: for nullable Guid, null != teamId is TRUE — an
        // unknown-offense drive would silently join the opponent average.
        var oppStarts = drives
            .Where(d => d.StartFranchiseSeasonId.HasValue
                     && d.StartFranchiseSeasonId != teamId
                     && d.StartYardsToEndzone.HasValue)
            .Select(d => 100m - d.StartYardsToEndzone!.Value)
            .ToList();

        if (myStarts.Count == 0 || oppStarts.Count == 0)
            return 0m;

        return Math.Round(myStarts.Average() - oppStarts.Average(), 2);
    }

    private static decimal CalculateTurnoverMarginPerDrive(
        Guid teamId,
        IReadOnlyCollection<FootballCompetitionPlay> plays,
        IReadOnlyCollection<CompetitionDrive> drives)
    {
        if (!plays.Any() || !drives.Any())
            return 0m;

        // AUDIT M1: one shared turnover type set (now includes
        // FumbleReturnTouchdown), and turnovers GAINED requires a known
        // opponent offense — for nullable Guids `null != teamId` is TRUE,
        // so an unknown-possession play would silently count as gained.

        // Plays where *this* team lost possession
        var turnoversLost = plays.Count(p =>
            p.StartFranchiseSeasonId == teamId && IsTurnoverType(p.Type));

        // Plays where *opponent* lost possession = turnovers gained
        var turnoversGained = plays.Count(p =>
            p.StartFranchiseSeasonId.HasValue &&
            p.StartFranchiseSeasonId.Value != teamId &&
            IsTurnoverType(p.Type));

        // Count of drives *started* by this team
        var offensiveDrives = drives.Count(d => d.StartFranchiseSeasonId == teamId);

        if (offensiveDrives == 0)
            return 0m;

        var margin = turnoversGained - turnoversLost;
        return Math.Round((decimal)margin / offensiveDrives, 4);
    }


    // AUDIT H3: CalculatePenaltyYardsPerPlay was REMOVED, not fixed —
    // ESPN's play document carries only the possessing offense
    // (StartFranchiseSeasonId), never the penalized team, so the metric
    // attributed the opponent's defensive penalties to the offense. If
    // penalty attribution ever becomes available, reintroduce with
    // signed-by-beneficiary yards and the fixture list in the audit doc.

    /// <summary>
    /// SHA-256 over the ordered plays — identity AND the content fields
    /// the formulas read — plus drive inputs and the final scoreboard
    /// (audit doc: recompute contract). Identity alone would treat an
    /// ESPN stat correction (same play list, revised yardage/down/type)
    /// or a drive backfill as "unchanged" and leave stale metrics.
    /// Recompute may skip a competition whose stored
    /// (InputsHash, FormulaVersion) both match the current values.
    /// </summary>
    private static string? ComputeInputsHash(
        List<FootballCompetitionPlay> orderedPlays,
        IReadOnlyCollection<CompetitionDrive> drives)
    {
        if (orderedPlays.Count == 0)
            return null;

        var last = orderedPlays[^1];
        var sb = new StringBuilder();
        foreach (var play in orderedPlays)
        {
            sb.Append(play.EspnId).Append(',')
              .Append((int)play.Type).Append(',')
              .Append(play.StatYardage).Append(',')
              .Append(play.StartDown).Append(',')
              .Append(play.StartDistance).Append(',')
              .Append(play.StartYardsToEndzone).Append(',')
              .Append(play.PeriodNumber).Append(',')
              .Append(play.ClockValue).Append(',')
              .Append(play.HomeScore).Append(',')
              .Append(play.AwayScore).Append(',')
              .Append(play.StartFranchiseSeasonId).Append(',')
              .Append(play.DriveId).Append('|');
        }
        foreach (var drive in drives.OrderBy(d => d.Ordinal).ThenBy(d => d.Id))
        {
            sb.Append(drive.Id).Append(',')
              .Append(drive.StartFranchiseSeasonId).Append(',')
              .Append(drive.StartYardsToEndzone).Append('|');
        }
        sb.Append(last.HomeScore).Append(':').Append(last.AwayScore);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }


    private decimal CalculateYpp(Guid franchiseSeasonId, List<FootballCompetitionPlay> plays)
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
    private decimal CalculateSuccessRate(Guid franchiseSeasonId, List<FootballCompetitionPlay> plays)
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
        List<FootballCompetitionPlay> plays,
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
    private decimal CalculateThirdFourthConversionRate(Guid franchiseSeasonId, List<FootballCompetitionPlay> plays)
    {
        var snaps = plays.Where(p => IsOffensiveScrimmageSnap(p, franchiseSeasonId)
                                     && (p.StartDown == 3 || p.StartDown == 4))
            .ToList();

        if (snaps.Count == 0) return 0m;

        var conversions = snaps.Count(p => AchievedFirstDownByYardage(p));
        return (decimal)conversions / snaps.Count;
    }

    // Points per drive from the cumulative scoreboard on plays.
    //
    // AUDIT FIX (C1): drive points = score at the drive's LAST play minus
    // the score BEFORE the drive's FIRST play (the last play anywhere in
    // the game preceding it; 0 only at true game start). The previous
    // implementation used the drive's second-to-last play as the
    // baseline — and for a ONE-PLAY drive that baseline degenerated to
    // 0, crediting the drive with the team's entire cumulative score
    // (a kneel-down while leading 42-7 booked +42), which inflated
    // season PPD to physically impossible values (~6).
    private decimal CalculatePointsPerDrive(
        Guid franchiseSeasonId,
        List<FootballCompetitionPlay> plays,
        Guid homeFsId)
    {
        int ScoreOf(FootballCompetitionPlay p) =>
            franchiseSeasonId == homeFsId ? p.HomeScore : p.AwayScore;

        var ordered = OrderPlays(
            plays.Where(p => p.DriveId.HasValue && p.DriveId != Guid.Empty));

        var drives = ordered
            .Where(p => p.StartFranchiseSeasonId == franchiseSeasonId)
            .Select((p, _) => p)
            .GroupBy(p => p.DriveId!.Value)
            .Select(g => new
            {
                FirstIndex = ordered.IndexOf(g.First()),
                Last = g.Last()
            })
            .OrderBy(d => d.FirstIndex)
            .ToList();

        if (drives.Count == 0) return 0m;

        var totalPoints = 0;

        foreach (var drive in drives)
        {
            var baseline = drive.FirstIndex > 0
                ? ScoreOf(ordered[drive.FirstIndex - 1])
                : 0;

            var drivePoints = ScoreOf(drive.Last) - baseline;

            // A single possession is bounded by [0, 8] (TD + 2pt). The
            // clamp guards scoreboard glitches in the source data rather
            // than trusting them into the season average.
            totalPoints += Math.Clamp(drivePoints, 0, 8);
        }

        return (decimal)totalPoints / drives.Count;
    }


    // AUDIT FIX (H2): both red-zone rates share ONE trip state machine
    // with the complete termination contract — the previous per-function
    // copies closed a trip ONLY on an opponent scrimmage snap, so a trip
    // left open across a possession change or halftime could credit a
    // LATER drive's score to the stale trip.

    // Red Zone TD Rate (null if no trips): TD-trips / trips
    private decimal? CalculateRedZoneTdRate(Guid franchiseSeasonId, List<FootballCompetitionPlay> plays)
    {
        var (trips, scoringTrips) = CountRedZoneTrips(
            franchiseSeasonId,
            plays,
            p => p.Type == PlayType.RushingTouchdown || p.Type == PlayType.PassingTouchdown);

        if (trips == 0) return null;
        return (decimal)scoringTrips / trips;
    }

    // Red Zone Scoring Rate (null if no trips): scoring-trips / trips
    // (TD or made field goal)
    private decimal? CalculateRedZoneScoringRate(Guid franchiseSeasonId, List<FootballCompetitionPlay> plays)
    {
        var (trips, scoringTrips) = CountRedZoneTrips(
            franchiseSeasonId,
            plays,
            p => p.Type == PlayType.RushingTouchdown
                 || p.Type == PlayType.PassingTouchdown
                 || p.Type == PlayType.FieldGoalGood);

        if (trips == 0) return null;
        return (decimal)scoringTrips / trips;
    }

    /// <summary>
    /// The shared red-zone trip state machine. A trip STARTS on this
    /// offense's first scrimmage snap at/inside the 20. Once open, it
    /// CLOSES on the FIRST of (audit H2 termination contract):
    ///   1. the opposing offense taking a standing scrimmage snap;
    ///   2. THIS offense starting a NEW drive (DriveId change) — covers
    ///      turnovers, defensive scores, and kickoff-separated
    ///      possessions in one rule;
    ///   3. a half boundary (Q2→Q3), regulation→OT, or any OT→OT break —
    ///      possessions do not survive those; Q1→Q2 and Q3→Q4 do NOT
    ///      terminate (drives legitimately span them);
    ///   4. end of input.
    /// Scoring counts only while the trip is open. Adjacent duplicate
    /// play events (same SequenceNumber) are processed once.
    /// </summary>
    private (int Trips, int ScoringTrips) CountRedZoneTrips(
        Guid franchiseSeasonId,
        List<FootballCompetitionPlay> plays,
        Func<FootballCompetitionPlay, bool> isScore)
    {
        var trips = 0;
        var scoringTrips = 0;
        var inTrip = false;
        var tripScored = false;
        Guid? tripDriveId = null;
        int? lastHalf = null;
        string? lastSequence = null;

        void CloseTrip()
        {
            if (tripScored) scoringTrips++;
            inTrip = false;
            tripScored = false;
            tripDriveId = null;
        }

        // Q1-Q2 = 1, Q3-Q4 = 2, then EVERY overtime period is its own
        // bucket: possessions do not span an OT break.
        static int HalfOf(FootballCompetitionPlay p)
            => p.PeriodNumber <= 2 ? 1 : p.PeriodNumber <= 4 ? 2 : p.PeriodNumber;

        foreach (var p in plays)
        {
            // duplicate event: same sequence as the previous play
            if (p.SequenceNumber == lastSequence) continue;
            lastSequence = p.SequenceNumber;

            // rule 3: half boundary / regulation→OT / OT→OT
            var half = HalfOf(p);
            if (inTrip && lastHalf.HasValue && half != lastHalf.Value)
            {
                CloseTrip();
            }
            lastHalf = half;

            // rule 2: this offense on a NEW drive — close the stale trip
            // BEFORE evaluating whether this play starts a fresh one
            if (inTrip
                && p.StartFranchiseSeasonId == franchiseSeasonId
                && p.DriveId.HasValue
                && tripDriveId.HasValue
                && p.DriveId.Value != tripDriveId.Value)
            {
                CloseTrip();
            }

            // trip start: this offense, scrimmage snap, at/inside the 20
            if (!inTrip
                && IsOffensiveScrimmageSnap(p, franchiseSeasonId)
                && p.StartYardsToEndzone.HasValue
                && p.StartYardsToEndzone.Value <= 20)
            {
                inTrip = true;
                tripScored = false;
                tripDriveId = p.DriveId;
                trips++;
            }

            if (!inTrip) continue;

            // scoring for THIS offense while the trip is open
            if (p.StartFranchiseSeasonId.HasValue
                && p.StartFranchiseSeasonId.Value == franchiseSeasonId
                && isScore(p))
            {
                tripScored = true;
            }

            // rule 1: the OTHER offense takes a standing scrimmage snap
            if (p.StartFranchiseSeasonId.HasValue
                && p.StartFranchiseSeasonId.Value != franchiseSeasonId
                && p.StartDown is >= 1 and <= 4
                && IsOffensiveScrimmageType(p.Type)
                && !(p.Text?.Contains("NO PLAY", StringComparison.OrdinalIgnoreCase) == true))
            {
                CloseTrip();
            }
        }

        // rule 4: end of input
        if (inTrip) CloseTrip();

        return (trips, scoringTrips);
    }

    /* ================= HELPERS ================ */

    // AUDIT FIX (H1): interception and lost-fumble plays keep the
    // intercepted/fumbling OFFENSE in StartFranchiseSeasonId, but their
    // StatYardage is the DEFENSIVE RETURN — usable for neither offensive
    // yardage nor first-down math. These plays join the snap
    // DENOMINATORS (they are offensive snaps with catastrophic
    // outcomes; excluding them flattered turnover-prone teams) at an
    // effective yardage of ZERO: never a success, never explosive,
    // never a conversion.
    // SequenceNumber is an ESPN STRING; lexicographic ordering breaks
    // when digit counts differ ("10" < "9"). Order numerically when
    // parseable, falling back to the raw string. Every order-sensitive
    // consumer (drive baselines, the red-zone trip machine) uses this
    // SAME ordering, so they cannot disagree.
    private static List<FootballCompetitionPlay> OrderPlays(IEnumerable<FootballCompetitionPlay> plays)
        => plays
            .OrderBy(p => long.TryParse(p.SequenceNumber, out var n) ? n : long.MaxValue)
            .ThenBy(p => p.SequenceNumber, StringComparer.Ordinal)
            .ToList();

    private static bool IsTurnoverType(PlayType t)
        => t == PlayType.PassInterceptionReturn
           || t == PlayType.InterceptionReturnTouchdown
           || t == PlayType.Interception
           || t == PlayType.FumbleLost
           || t == PlayType.FumbleReturnTouchdown;

    private static int Yardage(FootballCompetitionPlay p)
        => IsTurnoverType(p.Type) ? 0 : p.StatYardage;

    // first down purely by distance-to-gain (no FirstDown flag in your data)
    private static bool AchievedFirstDownByYardage(FootballCompetitionPlay p)
        => p.StartDistance.HasValue && Yardage(p) >= p.StartDistance.Value;

    // which team is on offense at the snap? (null means unknown → not this offense)
    private static bool IsOffense(FootballCompetitionPlay p, Guid franchiseSeasonId)
        => p.StartFranchiseSeasonId.HasValue
           && p.StartFranchiseSeasonId.Value == franchiseSeasonId;

    // helper: which play types count as *offensive scrimmage snaps* for team metrics?
    private static bool IsOffensiveScrimmageType(PlayType t)
    {
        // Include normal snaps from scrimmage:
        // - Rush / Pass (comp & inc) / Sack
        // - TD variants that ESPN sometimes logs as distinct types
        // - Safety (comes from a scrimmage snap)
        // - AUDIT H1: turnover plays (interception / lost fumble) — they
        //   ARE scrimmage snaps by the offense; Yardage() zeroes their
        //   defensive-return StatYardage so denominators grow but
        //   numerators never see return yards.
        return t == PlayType.Rush
               || t == PlayType.RushingTouchdown
               || t == PlayType.PassReception
               || t == PlayType.PassingTouchdown
               || t == PlayType.PassIncompletion
               || t == PlayType.Sack
               || t == PlayType.Safety
               || IsTurnoverType(t);
    }

    // full filter for "counts toward offense's snaps"
    private static bool IsOffensiveScrimmageSnap(FootballCompetitionPlay p, Guid franchiseSeasonId)
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
