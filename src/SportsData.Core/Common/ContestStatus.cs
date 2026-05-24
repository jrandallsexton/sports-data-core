using System;

namespace SportsData.Core.Common
{
    public enum ContestStatus
    {
        Undefined = 0,
        Canceled = 1,
        Completed = 2,
        Delayed = 3,
        Ongoing = 4,
        Postponed = 5,
        Scheduled = 6,
        Suspended = 7,
        Final = 8,
        InProgress = 9,
        Halftime = 10
    }

    public static class ContestStatusValues
    {
        // ── Canonical wire shape (raw ESPN status type names) ───────────────
        // What Producer ships on the canonical Matchup.Status going forward.
        // Use these for any new code that needs to test for completion via a
        // string comparison.
        public const string FinalRaw = "STATUS_FINAL";
        public const string CompletedRaw = "STATUS_COMPLETED";

        // ── Legacy PascalCase enum-name forms ────────────────────────────────
        // What the wire used to ship pre-dual-field PR (via the
        // REPLACE(StatusDescription, ' ', '') hack and replay services'
        // nameof(ContestStatus.X) publishes). Kept here for transition
        // tolerance only — `IsCompleted` accepts both forms so existing
        // callers don't break. Do NOT use these in new code; prefer the
        // canonical raw constants above.
        public const string Final = "Final";
        public const string Completed = "Completed";

        public static bool IsCompleted(string? status) =>
            string.Equals(status, FinalRaw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, CompletedRaw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, Final, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);
    }
}
