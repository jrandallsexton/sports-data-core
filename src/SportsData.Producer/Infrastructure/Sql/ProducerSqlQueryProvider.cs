using SportsData.Producer.Application.Contests.Queries.Matchups;

namespace SportsData.Producer.Infrastructure.Sql;

public class ProducerSqlQueryProvider
{
    private static readonly string[] _fileNames =
    [
        "GetCompletedFbsContestIds.sql",
        "GetFinalizedContestIds.sql",
        "GetMatchupResultByContestId.sql",
        "GetContestResultsByContestIds.sql",
        "GetMatchupsByContestIds.sql",
        "GetMatchupForPreview.sql",
        "GetMatchupForPreviewBatch.sql",
        "GetMatchupsForCurrentWeek.sql",
        "GetMatchupsForSeasonWeek.sql",
        "GetMatchupByContestId.sql",
        "GetRankingsByPollByWeek.sql",
        "GetFranchiseSeasonCompetitionResults.sql",
        "GetFranchiseSeasonPreviewStats.sql",
        "GetFranchiseSeasonStatistics.sql",
        "GetConferenceIdsBySlugs.sql",
        "GetConferenceNamesAndSlugs.sql",
        "GetTeamCard.sql",
        "GetTeamCardSchedule.sql",
        "GetTeamSeasons.sql"
    ];

    private readonly Dictionary<string, string> _queries = new();

    public ProducerSqlQueryProvider()
    {
        var assembly = typeof(ProducerSqlQueryProvider).Assembly;
        var preferredId = MatchupSqlBuilder.PreferredOddsProviderId.ToString();
        var fallbackId = MatchupSqlBuilder.FallbackOddsProviderId.ToString();

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

            // Replace provider ID placeholders with actual values
            query = query
                .Replace("{PreferredOddsProviderId}", preferredId)
                .Replace("{FallbackOddsProviderId}", fallbackId);

            _queries.Add(fileName, query);
        }
    }

    private string Get(string fileName)
    {
        if (!_queries.TryGetValue(fileName, out var sql))
            throw new KeyNotFoundException($"SQL query not found: {fileName}");

        return sql;
    }

    public string GetCompletedFbsContestIds() => Get("GetCompletedFbsContestIds.sql");

    public string GetFinalizedContestIds() => Get("GetFinalizedContestIds.sql");

    public string GetMatchupResultByContestId() => Get("GetMatchupResultByContestId.sql");

    public string GetContestResultsByContestIds() => Get("GetContestResultsByContestIds.sql");

    public string GetMatchupsByContestIds() => Get("GetMatchupsByContestIds.sql");

    public string GetMatchupForPreview() => Get("GetMatchupForPreview.sql");

    public string GetMatchupForPreviewBatch() => Get("GetMatchupForPreviewBatch.sql");

    public string GetMatchupsForCurrentWeek() => Get("GetMatchupsForCurrentWeek.sql");

    public string GetMatchupsForSeasonWeek() => Get("GetMatchupsForSeasonWeek.sql");

    public string GetMatchupByContestId() => Get("GetMatchupByContestId.sql");

    public string GetRankingsByPollByWeek() => Get("GetRankingsByPollByWeek.sql");

    public string GetFranchiseSeasonCompetitionResults() => Get("GetFranchiseSeasonCompetitionResults.sql");

    public string GetFranchiseSeasonPreviewStats() => Get("GetFranchiseSeasonPreviewStats.sql");

    public string GetFranchiseSeasonStatistics() => Get("GetFranchiseSeasonStatistics.sql");

    public string GetConferenceIdsBySlugs() => Get("GetConferenceIdsBySlugs.sql");

    public string GetConferenceNamesAndSlugs() => Get("GetConferenceNamesAndSlugs.sql");

    public string GetTeamCard() => Get("GetTeamCard.sql");

    public string GetTeamCardSchedule() => Get("GetTeamCardSchedule.sql");

    public string GetTeamSeasons() => Get("GetTeamSeasons.sql");
}
