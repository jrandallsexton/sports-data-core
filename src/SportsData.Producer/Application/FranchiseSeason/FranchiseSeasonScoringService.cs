//using Microsoft.EntityFrameworkCore;
//using SportsData.Producer.Infrastructure.Data.Common;

//namespace SportsData.Producer.Application.FranchiseSeason
//{
//    public class FranchiseSeasonScoringService
//    {
//    }

//    [Keyless]
//    public record TeamGameOffenseAgg(
//        Guid ContestId,
//        Guid TeamId,
//        int Season,
//        int Plays,
//        decimal Yards,
//        decimal Ypp,
//        decimal SuccessRate,
//        decimal ExplosiveRate,
//        int Drives,
//        int Points,
//        decimal PointsPerDrive,
//        int ThirdFourthAtt,
//        int ThirdFourthConv,
//        decimal ThirdFourthRate,
//        int RzTrips,
//        int RzTds,
//        decimal? RzTdRate,
//        decimal PenaltyYardsPerPlay,
//        decimal AvgStartYardline
//    );

//    [Keyless]
//    public record TeamGameDefenseAgg( // opponent's offense vs this team
//        Guid ContestId,
//        Guid TeamId,           // same team as above
//        int Season,
//        decimal OppYpp,
//        decimal OppSuccessRate,
//        decimal OppExplosiveRate,
//        decimal OppPointsPerDrive,
//        decimal OppThirdFourthRate,
//        decimal? OppRzTdRate
//    );

//    [Keyless]
//    public record TeamGameSpecialTeamsAgg(
//        Guid ContestId,
//        Guid TeamId,
//        int Season,
//        decimal NetPunt,        // avg net
//        int FgMakes,
//        int FgAttempts,
//        decimal FgPctShrunk,    // (makes+1)/(atts+2)
//        decimal FieldPosDiff    // avgStart(team) - avgStart(opp)
//    );

//    [Keyless]
//    public record TeamGameDisciplineAgg(
//        Guid ContestId,
//        Guid TeamId,
//        int Season,
//        int Takeaways,
//        int Giveaways,
//        int TotalDrives,
//        decimal TurnoverMarginPerDrive
//    );

//    [Keyless]
//    public record TeamGameRawMetrics( // convenience join of everything you need to score TGS
//        Guid ContestId,
//        Guid TeamId,
//        int Season,

//        // Offense
//        decimal Ypp,
//        decimal SuccessRate,
//        decimal ExplosiveRate,
//        decimal PointsPerDrive,
//        decimal ThirdFourthRate,
//        decimal? RzTdRate,

//        // Defense (opponent perspective)
//        decimal OppYpp,
//        decimal OppSuccessRate,
//        decimal OppExplosiveRate,
//        decimal OppPointsPerDrive,
//        decimal OppThirdFourthRate,
//        decimal? OppRzTdRate,

//        // ST / Discipline
//        decimal NetPunt,
//        decimal FgPctShrunk,
//        decimal FieldPosDiff,
//        decimal TurnoverMarginPerDrive,
//        decimal PenaltyYardsPerPlay
//    );

//    [Keyless]
//    public record MetricDistributionRow(
//        int Season,
//        string MetricName,
//        decimal P5,
//        decimal P95,
//        DateTime ComputedUtc
//    );

//    public static class TeamGameQueries
//    {
//        // --- Offense aggregation per (Contest, Team) ---
//        public static IQueryable<TeamGameOffenseAgg> BuildOffenseAgg(TeamSportDataContext db)
//        {
//            var teamPlays =
//                from p in db.Plays
//                group p by new { p.ContestId, p.TeamId, p.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    Plays = g.Count(),
//                    Yards = (decimal)g.Sum(x => x.YardsGained),
//                    Successes = g.Sum(x =>
//                        (x.FirstDown ? 1 :
//                         x.Down == 1 && x.YardsGained >= 7 ? 1 :
//                         x.Down == 2 && x.YardsGained >= 4 ? 1 :
//                         (x.Down == 3 || x.Down == 4) && x.YardsGained >= 2 ? 1 : 0)),
//                    Explosives = g.Sum(x => x.YardsGained >= 20 ? 1 : 0),
//                    ThirdFourthAtt = g.Sum(x => (x.Down == 3 || x.Down == 4) ? 1 : 0),
//                    ThirdFourthConv = g.Sum(x => (x.Down == 3 || x.Down == 4) && x.FirstDown ? 1 : 0),
//                    PenaltyYards = (decimal)g.Sum(x => x.PenaltyYards ?? 0)
//                };

//            var redZone =
//                from s in db.Series // or Drives if RZ trips are counted there
//                group s by new { s.ContestId, s.TeamId, s.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    RzTrips = g.Count(x => x.StartYardline <= 20),
//                    RzTds = g.Count(x => x.StartYardline <= 20 && x.Result == "TD")
//                };

//            var drives =
//                from d in db.Drives
//                group d by new { d.ContestId, d.TeamId, d.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    Drives = g.Count(),
//                    Points = g.Sum(x => x.Points),
//                    AvgStart = (decimal)g.Average(x => x.StartYardline)
//                };

//            var q =
//                from tp in teamPlays
//                join dr in drives on new { tp.ContestId, tp.TeamId, tp.Season } equals new { dr.ContestId, dr.TeamId, dr.Season } into jdr
//                from dr in jdr.DefaultIfEmpty()
//                join rz in redZone on new { tp.ContestId, tp.TeamId, tp.Season } equals new { rz.ContestId, rz.TeamId, rz.Season } into jrz
//                from rz in jrz.DefaultIfEmpty()
//                select new TeamGameOffenseAgg(
//                    tp.ContestId,
//                    tp.TeamId,
//                    tp.Season,
//                    tp.Plays,
//                    tp.Yards,
//                    Ypp: NullSafe(tp.Yards, tp.Plays),
//                    SuccessRate: NullSafe(tp.Successes, tp.Plays),
//                    ExplosiveRate: NullSafe(tp.Explosives, tp.Plays),
//                    Drives: dr?.Drives ?? 0,
//                    Points: dr?.Points ?? 0,
//                    PointsPerDrive: NullSafe(dr?.Points ?? 0, dr?.Drives ?? 0),
//                    ThirdFourthAtt: tp.ThirdFourthAtt,
//                    ThirdFourthConv: tp.ThirdFourthConv,
//                    ThirdFourthRate: NullSafe(tp.ThirdFourthConv, tp.ThirdFourthAtt),
//                    RzTrips: rz?.RzTrips ?? 0,
//                    RzTds: rz?.RzTds ?? 0,
//                    RzTdRate: (rz is null || rz.RzTrips == 0) ? (decimal?)null : (decimal)rz.RzTds / rz.RzTrips,
//                    PenaltyYardsPerPlay: NullSafe(tp.PenaltyYards, tp.Plays),
//                    AvgStartYardline: dr?.AvgStart ?? 0m
//                );

//            return q;

//            static decimal NullSafe(decimal num, int den) => den == 0 ? 0m : num / den;
//            static decimal NullSafe(int num, int den) => den == 0 ? 0m : (decimal)num / den;
//        }

//        // --- Defense aggregation = opponent's offense vs same contest ---
//        public static IQueryable<TeamGameDefenseAgg> BuildDefenseAgg(TeamSportDataContext db)
//        {
//            var off = BuildOffenseAgg(db);

//            // Need contest pairs (team vs opp) – from ContestCompetitor table
//            var pairs =
//                from cc1 in db.ContestCompetitors
//                join cc2 in db.ContestCompetitors on cc1.ContestId equals cc2.ContestId
//                where cc1.TeamId != cc2.TeamId
//                select new { cc1.ContestId, TeamId = cc1.TeamId, OpponentId = cc2.TeamId };

//            var q =
//                from p in pairs
//                join oppOff in off on new { p.ContestId, TeamId = p.OpponentId } equals new { oppOff.ContestId, oppOff.TeamId }
//                select new TeamGameDefenseAgg(
//                    oppOff.ContestId,
//                    p.TeamId,
//                    oppOff.Season,
//                    OppYpp: oppOff.Ypp,
//                    OppSuccessRate: oppOff.SuccessRate,
//                    OppExplosiveRate: oppOff.ExplosiveRate,
//                    OppPointsPerDrive: oppOff.PointsPerDrive,
//                    OppThirdFourthRate: oppOff.ThirdFourthRate,
//                    OppRzTdRate: oppOff.RzTdRate
//                );

//            return q;
//        }

//        // --- Special teams (net punt, FG shrunk, field position diff) ---
//        public static IQueryable<TeamGameSpecialTeamsAgg> BuildSpecialTeamsAgg(TeamSportDataContext db)
//        {
//            var punts =
//                from k in db.KickSummaries
//                where k.Type == "Punt"
//                group k by new { k.ContestId, k.TeamId, k.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    NetPunt = (decimal)g.Average(x => x.PuntNetYards)
//                };

//            var fgs =
//                from k in db.KickSummaries
//                where k.Type == "FieldGoal"
//                group k by new { k.ContestId, k.TeamId, k.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    Makes = g.Count(x => x.IsMade),
//                    Attempts = g.Count()
//                };

//            var avgStart =
//                from d in db.Drives
//                group d by new { d.ContestId, d.TeamId, d.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    AvgStart = (decimal)g.Average(x => x.StartYardline)
//                };

//            // Opponent avgStart for field position diff
//            var pairs =
//                from cc1 in db.ContestCompetitors
//                join cc2 in db.ContestCompetitors on cc1.ContestId equals cc2.ContestId
//                where cc1.TeamId != cc2.TeamId
//                select new { cc1.ContestId, TeamId = cc1.TeamId, OpponentId = cc2.TeamId };

//            var q =
//                from p in pairs
//                join a in avgStart on new { p.ContestId, p.TeamId } equals new { a.ContestId, a.TeamId }
//                join o in avgStart on new { p.ContestId, TeamId = p.OpponentId } equals new { o.ContestId, o.TeamId }
//                join pu in punts on new { p.ContestId, p.TeamId } equals new { pu.ContestId, pu.TeamId } into jpu
//                from pu in jpu.DefaultIfEmpty()
//                join fg in fgs on new { p.ContestId, p.TeamId } equals new { fg.ContestId, fg.TeamId } into jfg
//                from fg in jfg.DefaultIfEmpty()
//                select new TeamGameSpecialTeamsAgg(
//                    a.ContestId,
//                    a.TeamId,
//                    a.Season,
//                    NetPunt: pu?.NetPunt ?? 0m,
//                    FgMakes: fg?.Makes ?? 0,
//                    FgAttempts: fg?.Attempts ?? 0,
//                    FgPctShrunk: (decimal)((((fg?.Makes ?? 0) + 1.0) / (((fg?.Attempts ?? 0) + 2.0)))),
//                    FieldPosDiff: a.AvgStart - o.AvgStart
//                );

//            return q;
//        }

//        // --- Discipline (turnovers, penalties, per-drive) ---
//        public static IQueryable<TeamGameDisciplineAgg> BuildDisciplineAgg(TeamSportDataContext db)
//        {
//            var drives =
//                from d in db.Drives
//                group d by new { d.ContestId, d.TeamId, d.Season } into g
//                select new { g.Key.ContestId, g.Key.TeamId, g.Key.Season, Drives = g.Count() };

//            var tos =
//                from p in db.Plays
//                group p by new { p.ContestId, p.TeamId, p.Season } into g
//                select new
//                {
//                    g.Key.ContestId,
//                    g.Key.TeamId,
//                    g.Key.Season,
//                    Takeaways = g.Sum(x => x.IsTakeaway ? 1 : 0),
//                    Giveaways = g.Sum(x => x.IsTurnover ? 1 : 0)
//                };

//            var q =
//                from t in tos
//                join d in drives on new { t.ContestId, t.TeamId, t.Season } equals new { d.ContestId, d.TeamId, d.Season } into jd
//                from d in jd.DefaultIfEmpty()
//                select new TeamGameDisciplineAgg(
//                    t.ContestId,
//                    t.TeamId,
//                    t.Season,
//                    t.Takeaways,
//                    t.Giveaways,
//                    TotalDrives: d?.Drives ?? 0,
//                    TurnoverMarginPerDrive: (d == null || d.Drives == 0) ? 0m : (decimal)(t.Takeaways - t.Giveaways) / d.Drives
//                );

//            return q;
//        }

//        // --- Final “raw metrics” join for scoring ---
//        public static IQueryable<TeamGameRawMetrics> BuildTeamGameRaw(TeamSportDataContext db)
//        {
//            var off = BuildOffenseAgg(db);
//            var def = BuildDefenseAgg(db);
//            var st = BuildSpecialTeamsAgg(db);
//            var dis = BuildDisciplineAgg(db);

//            var q =
//                from o in off
//                join d in def on new { o.ContestId, o.TeamId } equals new { d.ContestId, d.TeamId }
//                join s in st on new { o.ContestId, o.TeamId } equals new { s.ContestId, s.TeamId }
//                join di in dis on new { o.ContestId, o.TeamId } equals new { di.ContestId, di.TeamId }
//                select new TeamGameRawMetrics(
//                    o.ContestId,
//                    o.TeamId,
//                    o.Season,
//                    o.Ypp,
//                    o.SuccessRate,
//                    o.ExplosiveRate,
//                    o.PointsPerDrive,
//                    o.ThirdFourthRate,
//                    o.RzTdRate,
//                    d.OppYpp,
//                    d.OppSuccessRate,
//                    d.OppExplosiveRate,
//                    d.OppPointsPerDrive,
//                    d.OppThirdFourthRate,
//                    d.OppRzTdRate,
//                    s.NetPunt,
//                    s.FgPctShrunk,
//                    s.FieldPosDiff,
//                    di.TurnoverMarginPerDrive,
//                    o.PenaltyYardsPerPlay
//                );

//            return q;
//        }
//    }

//}
