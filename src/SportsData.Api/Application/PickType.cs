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
}