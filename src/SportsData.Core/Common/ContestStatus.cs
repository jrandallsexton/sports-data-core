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

        public static bool IsCompleted(string? status) =>
            string.Equals(status, Final, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(status, Completed, StringComparison.OrdinalIgnoreCase);
    }
}
