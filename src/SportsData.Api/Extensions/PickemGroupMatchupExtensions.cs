using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Extensions
{
    public static class PickemGroupMatchupExtensions
    {
        public static bool IsLocked(this PickemGroupMatchup matchup, DateTime? nowUtc = null)
            => IsStartLocked(matchup.StartDateUtc, nowUtc);

        /// <summary>
        /// Core lock rule, expressed over a raw start time so read queries can
        /// project just <c>StartDateUtc</c> instead of materializing the full
        /// entity. <see cref="IsLocked(PickemGroupMatchup, DateTime?)"/> delegates here.
        /// </summary>
        public static bool IsStartLocked(DateTime startDateUtc, DateTime? nowUtc = null)
        {
            var referenceTime = nowUtc ?? DateTime.UtcNow;

            // Lock the matchup if it starts within 5 minutes or already started
            return startDateUtc <= referenceTime.AddMinutes(5);
        }
    }
}