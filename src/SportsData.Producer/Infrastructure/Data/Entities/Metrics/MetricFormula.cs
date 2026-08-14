namespace SportsData.Producer.Infrastructure.Data.Entities.Metrics
{
    /// <summary>
    /// The single formula vintage stamp persisted on both
    /// CompetitionMetric and FranchiseSeasonMetric. Bump when EITHER the
    /// per-game formulas or the aggregation change — they ship as one
    /// vintage (docs/audit/competition-metrics-formula-audit.md,
    /// "Recompute contract"). Rows without a stamp predate stamping and
    /// are treated as stale by recompute.
    /// </summary>
    public static class MetricFormula
    {
        public const string Version = "2026.08";
    }
}
