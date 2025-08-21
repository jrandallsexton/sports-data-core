using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Extensions
{
    public static class PickemGroupMatchupExtensions
    {
        public static bool IsLocked(this PickemGroupMatchup matchup, DateTime? nowUtc = null)
        {
            var referenceTime = nowUtc ?? DateTime.UtcNow;

            // Lock the matchup if it starts within 5 minutes or already started
            return matchup.StartDateUtc <= referenceTime.AddMinutes(5);
        }
    }
}