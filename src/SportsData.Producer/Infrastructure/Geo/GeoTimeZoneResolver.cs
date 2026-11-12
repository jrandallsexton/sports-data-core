using GeoTimeZone;

namespace SportsData.Producer.Infrastructure.Geo
{
    public interface IGeoTimeZoneResolver
    {
        /// <summary>
        /// IANA time zone id (e.g. "America/Chicago") for a coordinate, or null
        /// when the coordinate is unset (0,0 is how an ungeocoded venue reads).
        /// </summary>
        string? Resolve(decimal latitude, decimal longitude);
    }

    /// <summary>
    /// Offline lookup against GeoTimeZone's embedded boundary data (Timezone
    /// Boundary Builder): no network call, so it is safe to run for every
    /// venue in one pass. Correct for states split across zones (IN, KY, TN,
    /// the FL panhandle), which a state map is not.
    /// </summary>
    public class GeoTimeZoneResolver : IGeoTimeZoneResolver
    {
        public string? Resolve(decimal latitude, decimal longitude)
        {
            if (latitude == 0 || longitude == 0)
                return null;

            var zone = TimeZoneLookup.GetTimeZone((double)latitude, (double)longitude).Result;
            return string.IsNullOrWhiteSpace(zone) ? null : zone;
        }
    }
}
