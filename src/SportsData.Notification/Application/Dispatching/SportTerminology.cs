using SportsData.Core.Common;

namespace SportsData.Notification.Application.Dispatching
{
    /// <summary>
    /// Maps <see cref="Sport"/> to user-facing push-copy fragments. Lives here
    /// (next to the dispatcher) rather than in Core because the only consumers
    /// are notification bodies, and Core has no business knowing how MLB calls
    /// the start of a game vs how the NBA does.
    ///
    /// <para>
    /// Sports not covered fall through to a generic label. Adding a sport is
    /// a one-line switch entry — no plumbing changes elsewhere.
    /// </para>
    /// </summary>
    public static class SportTerminology
    {
        public static (string Title, string Body) GetContestStartCopy(Sport sport)
        {
            return sport switch
            {
                Sport.BaseballMlb => (
                    "First pitch soon",
                    "A game you've picked has first pitch in about 30 minutes. Tap to follow live."),
                Sport.BasketballNba => (
                    "Tip-off soon",
                    "A game you've picked tips off in about 30 minutes. Tap to follow live."),
                Sport.FootballNcaa or Sport.FootballNfl => (
                    "Kickoff soon",
                    "A game you've picked kicks off in about 30 minutes. Tap to follow live."),
                Sport.GolfPga => (
                    "Round starting soon",
                    "A round you've picked tees off in about 30 minutes. Tap to follow live."),
                _ => (
                    "Game starting soon",
                    "A game you've picked starts in about 30 minutes. Tap to follow live.")
            };
        }
    }
}
