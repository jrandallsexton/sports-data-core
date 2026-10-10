namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>A fact that survived selection, rendered and ready to persist.</summary>
public record SelectedFact(
    PlayFact Fact,
    int Ordinal,
    int Priority,
    string Text,
    int TemplateVersion);

public record FactSelection(IReadOnlyList<SelectedFact> Selected, IReadOnlyList<DroppedFact> Dropped);

/// <summary>
/// The two teams of the play and, per team, when its previous game this season
/// was finalized. A team with no entry has no previous game; an entry with a
/// null value has one that isn't finalized yet.
/// </summary>
public record TeamFreshness(
    Guid? Offense,
    Guid? Defense,
    IReadOnlyDictionary<Guid, DateTime?> PreviousGameFinalizedUtc);

/// <summary>
/// Selects and orders a play's facts (instant-analysis task 07). v1 lets every
/// correct fact flow: priority orders, never filters. The only drops are the
/// freshness guard (a season document written before the athlete's team's
/// previous game was finalized can't include that game, so its count is short)
/// and facts that can't be phrased. Every drop carries a reason for the logger.
/// </summary>
public static class FactSelector
{
    /// <summary>
    /// Display order, lowest first; in code for now (decision 5). Unknown fact
    /// types sort last.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> Priorities = new Dictionary<string, int>
    {
        [FactTypes.SeasonLongest] = 10,
        [FactTypes.SeasonCount] = 20,
        [FactTypes.GameLongest] = 30,
        [FactTypes.GameCount] = 40,
        [FactTypes.TeamGameCount] = 50,
        [FactTypes.SeasonCountAgainst] = 60,
        [FactTypes.GameCountAgainst] = 70
    };

    private const int UnknownPriority = 1000;

    public static FactSelection Select(
        IEnumerable<PlayFact> facts,
        TeamFreshness teams,
        AnalysisNames names)
    {
        var dropped = new List<DroppedFact>();
        var kept = new List<(PlayFact Fact, int Priority, string Text)>();

        foreach (var fact in facts)
        {
            if (StaleReason(fact, teams) is { } stale)
            {
                dropped.Add(new DroppedFact(fact.FactType, fact.Kind, fact.AthleteSeasonId, stale));
                continue;
            }

            var text = FactPhraser.Render(fact, names, out var reason);
            if (text is null)
            {
                dropped.Add(new DroppedFact(fact.FactType, fact.Kind, fact.AthleteSeasonId, reason!));
                continue;
            }

            kept.Add((fact, Priorities.GetValueOrDefault(fact.FactType, UnknownPriority), text));
        }

        // Deterministic: priority, then kind, then the text itself.
        var selected = kept
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Fact.Kind)
            .ThenBy(x => x.Text, StringComparer.Ordinal)
            .Select((x, i) => new SelectedFact(x.Fact, i, x.Priority, x.Text, FactPhraser.TemplateVersion))
            .ToList();

        return new FactSelection(selected, dropped);
    }

    private static string? StaleReason(PlayFact fact, TeamFreshness teams)
    {
        // Game facts come from this game's own plays and are always current.
        if (fact.SeasonDocumentCreatedUtc is not { } documentUtc)
            return null;

        var team = AthleteSide(fact) == Side.Defense ? teams.Defense : teams.Offense;

        if (team is null)
            return "Athlete's team in this game is unknown, so the season document can't be checked for freshness.";

        if (!teams.PreviousGameFinalizedUtc.TryGetValue(team.Value, out var previousFinal))
            return null; // first game of the season: nothing earlier to miss

        if (previousFinal is null)
            return "Team's previous game isn't finalized, so the season document may be missing it.";

        return documentUtc < previousFinal
            ? $"Season document ({documentUtc:O}) predates the team's previous game being finalized ({previousFinal:O})."
            : null;
    }

    private enum Side { Offense, Defense }

    // Which side of this play the fact's athlete is on: actors of takeaways and
    // sacks are the defense; everyone else (scorers, explosive plays, victims) the offense.
    private static Side AthleteSide(PlayFact fact) =>
        fact.FactType == FactTypes.SeasonCount &&
        fact.Kind is PlayKind.Interception or PlayKind.Sack or PlayKind.FumbleLost
            ? Side.Defense
            : Side.Offense;
}
