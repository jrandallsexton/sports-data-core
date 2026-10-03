using System;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// One stored ESPN team-season statistic row, as the preview-stats SQL
    /// returns it. Nullable where the column is nullable: PerGameValue is
    /// absent for all but a handful of kicking statistics, and a non-nullable
    /// double here turned every absence into 0 (the all-zero preview stats
    /// bug, 2026-09-24).
    /// </summary>
    public class FranchiseSeasonRawStat
    {
        public string Category { get; set; } = default!;
        public string Statistic { get; set; } = default!;
        public double? Value { get; set; }
        public string? DisplayValue { get; set; }
        public double? PerGameValue { get; set; }
        public string? PerGameDisplayValue { get; set; }
        public int? Rank { get; set; }
    }
}
