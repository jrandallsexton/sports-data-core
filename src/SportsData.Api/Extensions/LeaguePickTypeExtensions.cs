using SportsData.Api.Application;

namespace SportsData.Api.Extensions
{
    public static class LeaguePickTypeExtensions
    {
        public static bool Includes(this PickType pickTypes, PickType flag) =>
            (pickTypes & flag) == flag;

        public static bool UsesConfidencePoints(this PickType pickTypes) =>
            pickTypes.Includes(PickType.Confidence);

        public static bool UsesAgainstTheSpread(this PickType pickTypes) =>
            pickTypes.Includes(PickType.AgainstTheSpread);

        public static bool UsesOverUnder(this PickType pickTypes) =>
            pickTypes.Includes(PickType.OverUnder);
    }

}
