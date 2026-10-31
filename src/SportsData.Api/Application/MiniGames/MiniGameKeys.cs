namespace SportsData.Api.Application.MiniGames;

/// <summary>
/// The mini-games the API accepts results for. Every game here today decides
/// its outcome by comparing the two scores; when one doesn't, that rule moves
/// to a per-game setting.
/// </summary>
public static class MiniGameKeys
{
    public const string FootballCards = "football-cards";

    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        FootballCards
    };
}
