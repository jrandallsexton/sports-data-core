namespace SportsData.Api.Application
{
    [Flags]
    public enum PickType
    {
        None = 0,
        StraightUp = 1,
        AgainstTheSpread = 2,
        OverUnder = 3
    }

    // TODO: Move usage of this to PickType
    public enum UserPickType
    {
        StraightUp = 1,
        AgainstTheSpread = 2,
        OverUnder = 3
    }
}