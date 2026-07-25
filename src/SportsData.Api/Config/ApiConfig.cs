namespace SportsData.Api.Config
{
    public class ApiConfig
    {
        public List<string> SupportedModes { get; set; } = new();

        public required string BaseUrl { get; set; }

        public required Guid UserIdSystem { get; set; }

        /// <summary>
        /// Per-sport instant at which league creation opens, keyed by <b>Sport enum
        /// name</b> (e.g. "FootballNcaa"). A sport present with a future value is blocked
        /// from creation until then (e.g. NCAAFB waits for AP Poll release ~Aug 17); a
        /// sport that is absent, or whose value is in the past, is open. Author values as
        /// ISO-8601 UTC (e.g. "2026-08-17T00:00:00Z" — a bare "2026-08-17T00:00:00" is
        /// also treated as UTC). Set per key in the API's AppConfig label:
        /// SportsData.Api:ApiConfig:LeagueCreationOpensUtc:{SportName} (e.g. :FootballNcaa).
        ///
        /// <para>
        /// String-keyed (not <c>Dictionary&lt;Sport, DateTime&gt;</c>) deliberately: the
        /// .NET configuration binder reliably populates string-keyed dictionaries (cf.
        /// <see cref="SportsData.Core.Config.CommonConfig.LoggingConfig.Overrides"/>),
        /// whereas enum-keyed dictionaries bind empty. <see cref="ILeagueCreationAvailability"/>
        /// parses the name → Sport and the value → a UTC instant.
        /// See docs/features/league-creation-availability-gate.md.
        /// </para>
        /// </summary>
        public Dictionary<string, string> LeagueCreationOpensUtc { get; set; } = new();
    }
}
