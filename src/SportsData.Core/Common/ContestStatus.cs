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
        public const string Final = "Final";
        public const string Completed = "Completed";

        // Raw ESPN status type names. Producer ships these verbatim on the
        // canonical Matchup.Status under the dual-field wire shape; the
        // PascalCase enum-name forms above stay supported during the
        // transition window in case any caller still passes them.
        public const string FinalRaw = "STATUS_FINAL";
        public const string CompletedRaw = "STATUS_COMPLETED";

        public static bool IsCompleted(string? status) =>
            string.Equals(status, Final, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, FinalRaw, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, CompletedRaw, StringComparison.OrdinalIgnoreCase);
    }
}
