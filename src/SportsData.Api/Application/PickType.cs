using System;

namespace SportsData.Api.Application
{
    [Flags]
    public enum PickType
    {
        None = 0,
        StraightUp = 1 << 0,  // 1
        AgainstTheSpread = 1 << 1,  // 2
        Confidence = 1 << 2,  // 4
        OverUnder = 1 << 3,  // 8

        // Future additions might include:
        // PlayoffBonus      = 1 << 4,  // 16
        // DoubleDown        = 1 << 5   // 32
    }
}