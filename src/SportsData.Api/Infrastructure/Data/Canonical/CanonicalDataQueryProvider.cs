namespace SportsData.Api.Infrastructure.Data.Canonical;

public class CanonicalDataQueryProvider
{
    private static readonly string[] _fileNames = [
        "GetCurrentSeasonWeek.sql",
        "GetLeagueMatchupsByContestIds.sql",
        "GetMatchupForPreviewGeneration.sql",
        "GetMatchupsForCurrentWeek.sql",
        "GetTeamCard.sql",
        "GetTeamCardSchedule.sql",
        "GetTeamSeasons.sql"
    ];

    private readonly Dictionary<string, string> _queries = new();

    public CanonicalDataQueryProvider()
    {
        var assembly = typeof(CanonicalDataQueryProvider).Assembly;

        foreach (var fileName in _fileNames)
        {
            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

            if (resourceName is null)
                throw new InvalidOperationException($"Embedded SQL resource not found: {fileName}");

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream);
            var query = reader.ReadToEnd();

            _queries.Add(fileName, query);
        }
    }

    private string Get(string fileName)
    {
        if (!_queries.TryGetValue(fileName, out var sql))
            throw new KeyNotFoundException($"SQL query not found: {fileName}");

        return sql;
    }

    public string GetCurrentSeasonWeek() => Get("GetCurrentSeasonWeek.sql");

    public string GetLeagueMatchupsByContestIds() => Get("GetLeagueMatchupsByContestIds.sql");

    public string GetMatchupForPreviewGeneration() => Get("GetMatchupForPreviewGeneration.sql");

    public string GetMatchupsForCurrentWeek() => Get("GetMatchupsForCurrentWeek.sql");

    public string GetTeamCard() => Get("GetTeamCard.sql");

    public string GetTeamCardSchedule() => Get("GetTeamCardSchedule.sql");

    public string GetTeamSeasons() => Get("GetTeamSeasons.sql");
}