namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// String forms of API's <c>PickType</c> enum, carried across services as
    /// strings (the same lighter-coupling convention as
    /// <c>PickemGroupMemberSnapshot.Role</c> — a cross-service string beats
    /// sharing the enum). These values are the <c>ToString()</c> of the API
    /// enum members; keep them in sync if API ever renames a member.
    /// </summary>
    public static class LeaguePickType
    {
        public const string StraightUp = "StraightUp";

        public const string AgainstTheSpread = "AgainstTheSpread";

        public const string OverUnder = "OverUnder";
    }
}
