namespace SportsData.Producer.Enums;

/// <summary>
/// Kickoff bucket on LOCAL (venue) time: Noon before 14:00, Afternoon
/// 14:00-17:59, Night from 18:00.
/// </summary>
public enum KickoffWindow
{
    Noon = 1,
    Afternoon = 2,
    Night = 3
}
