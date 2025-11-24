namespace SportsData.Api.Infrastructure.Data.Canonical;

public class CanonicalAdminDataQueryProvider
{
    private static readonly string[] _fileNames = [
        "CompetitionsWithoutCompetitors.sql",
        "CompetitionsWithoutPlays.sql",
        "CompetitionsWithoutDrives.sql",
        "CompetitionsWithoutMetrics.sql"
    ];

    private readonly Dictionary<string, string> _queries = new();

    public CanonicalAdminDataQueryProvider()
    {
        var assembly = typeof(CanonicalAdminDataQueryProvider).Assembly;

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

    public string GetCompetitionsWithoutCompetitors() => Get("CompetitionsWithoutCompetitors.sql");
    
    public string GetCompetitionsWithoutPlays() => Get("CompetitionsWithoutPlays.sql");
    
    public string GetCompetitionsWithoutDrives() => Get("CompetitionsWithoutDrives.sql");
    
    public string GetCompetitionsWithoutMetrics() => Get("CompetitionsWithoutMetrics.sql");
}