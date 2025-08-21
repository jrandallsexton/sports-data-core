namespace SportsData.Api.Application
{
    [Flags]
    public enum PickType
    {
        None = 0,
        StraightUp = 1 << 0,           // 1
        AgainstTheSpread = 1 << 1,     // 2
        OverUnder = 1 << 2             // 4 (future use)
    }

    public enum UserPickType
    {
        StraightUp = 1,
        AgainstTheSpread = 2,
        OverUnder = 3
    }
}