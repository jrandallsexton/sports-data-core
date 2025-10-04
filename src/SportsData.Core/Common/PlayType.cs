namespace SportsData.Core.Common
{
    public enum PlayType : int
    {
        CoinToss = 70,
        EndOfGame = 66,
        EndOfHalf = 65,
        EndPeriod = 2,

        FieldGoalGood = 59,
        FieldGoalMissed = 60,
        FieldGoalBlocked = 18,              // NEW
        FieldGoalMissedReturn = 40,         // NEW (miss + return)

        FumbleRecoveryOwn = 9,
        FumbleLost = 29,                    // NEW
        FumbleReturnTouchdown = 39,         // NEW

        Kickoff = 53,
        KickoffReturnOffense = 12,
        KickoffReturnTouchdown = 32,        // NEW

        PassIncompletion = 3,
        PassInterceptionReturn = 26,
        InterceptionReturnTouchdown = 36,   // NEW
        PassReception = 24,
        PassingTouchdown = 67,

        Penalty = 8,
        Punt = 52,
        PuntBlocked = 17,                   // NEW
        BlockedPuntReturnTouchdown = 37,    // NEW

        Rush = 5,
        RushingTouchdown = 68,

        Sack = 7,
        Timeout = 21,

        EndOfGameAlt = 79,                  // NEW (treat as EndOfGame)
        Safety = 20,                        // NEW

        Unknown = 9999
    }
}