using System.Globalization;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>Display names the templates need: athletes by AthleteSeasonId, teams by FranchiseSeasonId.</summary>
public record AnalysisNames(
    IReadOnlyDictionary<Guid, string> Athletes,
    IReadOnlyDictionary<Guid, string> Teams);

/// <summary>
/// Renders a <see cref="PlayFact"/> as one sentence (instant-analysis task 07).
/// Templates live here, in code, for the proof of concept (decision 2) and move
/// to a database catalog in Phase 2. Rendering is deterministic: the same fact
/// and names always give the same text. Bump <see cref="TemplateVersion"/>
/// whenever any template's wording changes, so persisted rows say which wording
/// they were rendered with.
/// </summary>
public static class FactPhraser
{
    public const int TemplateVersion = 1;

    /// <summary>The sentence, or null with a reason when there's no template or name.</summary>
    public static string? Render(PlayFact fact, AnalysisNames names, out string? reason)
    {
        reason = null;

        string? name = null;
        if (fact.FactType == FactTypes.TeamGameCount)
        {
            if (fact.FranchiseSeasonId is not { } team || !names.Teams.TryGetValue(team, out name))
            {
                reason = "No team name.";
                return null;
            }
        }
        else if (fact.AthleteSeasonId is not { } athlete || !names.Athletes.TryGetValue(athlete, out name))
        {
            reason = "No athlete name.";
            return null;
        }

        var text = Template(fact, name!);
        if (text is null)
            reason = $"No template for {fact.FactType}/{fact.Kind}.";

        return text;
    }

    private static string? Template(PlayFact fact, string name)
    {
        var whose = Possessive(name);
        var nth = Ordinal(fact.Value);
        var n = Number(fact.Value);

        return (fact.FactType, fact.Kind) switch
        {
            (FactTypes.GameCount, PlayKind.Interception) => $"{whose} {nth} interception of the game.",
            (FactTypes.GameCount, PlayKind.Sack) => $"{whose} {nth} sack of the game.",
            (FactTypes.GameCount, PlayKind.FumbleLost) => $"{whose} {nth} fumble recovery of the game.",
            (FactTypes.GameCount, PlayKind.PassingTouchdown) => $"{whose} {nth} touchdown pass of the game.",
            (FactTypes.GameCount, PlayKind.ReceivingTouchdown) => $"{whose} {nth} touchdown catch of the game.",
            (FactTypes.GameCount, PlayKind.RushingTouchdown) => $"{whose} {nth} rushing touchdown of the game.",

            (FactTypes.GameCountAgainst, PlayKind.Interception) => $"{whose} {nth} interception thrown this game.",
            (FactTypes.GameCountAgainst, PlayKind.Sack) => $"{name} has been sacked {Times(fact.Value)} this game.",
            (FactTypes.GameCountAgainst, PlayKind.FumbleLost) => $"{whose} {nth} lost fumble this game.",

            (FactTypes.TeamGameCount, PlayKind.Sack) => $"{whose} {nth} sack of the game.",
            (FactTypes.TeamGameCount, PlayKind.Interception or PlayKind.FumbleLost) => $"{whose} {nth} takeaway of the game.",

            (FactTypes.GameLongest, PlayKind.ExplosiveRun) => $"{whose} {n}-yard run is the longest of the game.",
            (FactTypes.GameLongest, PlayKind.ExplosivePass) => $"{whose} {n}-yard catch is the longest pass play of the game.",

            // ESPN credits split sacks as halves, so a season sack total can be 3.5: a count, not an ordinal.
            (FactTypes.SeasonCount, PlayKind.Sack) => $"{name} has {n} {Plural(fact.Value, "sack", "sacks")} this season.",
            (FactTypes.SeasonCount, PlayKind.Interception) => $"{whose} {nth} interception of the season.",
            (FactTypes.SeasonCount, PlayKind.FumbleLost) => $"{whose} {nth} fumble recovery of the season.",
            (FactTypes.SeasonCount, PlayKind.PassingTouchdown) => $"{whose} {nth} touchdown pass of the season.",
            (FactTypes.SeasonCount, PlayKind.ReceivingTouchdown) => $"{whose} {nth} touchdown catch of the season.",
            (FactTypes.SeasonCount, PlayKind.RushingTouchdown) => $"{whose} {nth} rushing touchdown of the season.",

            (FactTypes.SeasonCountAgainst, PlayKind.Interception) =>
                $"{name} has thrown {n} {Plural(fact.Value, "interception", "interceptions")} this season.",
            (FactTypes.SeasonCountAgainst, PlayKind.Sack) => $"{name} has been sacked {Times(fact.Value)} this season.",
            (FactTypes.SeasonCountAgainst, PlayKind.FumbleLost) =>
                $"{name} has lost {n} {Plural(fact.Value, "fumble", "fumbles")} this season.",

            (FactTypes.SeasonLongest, _) => SeasonLongest(fact, whose, n),

            _ => null
        };
    }

    // The season stat recorded in provenance says which long it beat.
    private static string? SeasonLongest(PlayFact fact, string whose, string yards) =>
        (fact.Provenance.TryGetValue("stat", out var stat) ? stat as string : null) switch
        {
            "rushing.longRushing" => $"{whose} longest run of the season ({yards} yards).",
            "passing.longPassing" => $"{whose} longest completion of the season ({yards} yards).",
            "receiving.longReception" => $"{whose} longest catch of the season ({yards} yards).",
            _ => null
        };

    private static readonly string[] OrdinalWords =
        ["first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth"];

    public static string Ordinal(decimal value)
    {
        var n = (int)decimal.Truncate(value);

        if (n >= 1 && n <= OrdinalWords.Length)
            return OrdinalWords[n - 1];

        var suffix = (n % 100) is 11 or 12 or 13
            ? "th"
            : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

        return n.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private static string Number(decimal value) =>
        value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Times(decimal value) =>
        value == 1 ? "once" : $"{Number(value)} times";

    private static string Plural(decimal value, string one, string many) =>
        value == 1 ? one : many;

    private static string Possessive(string name) =>
        name.EndsWith('s') ? name + "'" : name + "'s";
}
