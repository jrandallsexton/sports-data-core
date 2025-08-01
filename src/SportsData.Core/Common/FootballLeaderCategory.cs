using System;
using System.Linq;

namespace SportsData.Core.Common
{
    public enum FootballLeaderCategory
    {
        [LeaderCategory("Unknown", "UNK")]
        Unknown = 0,

        [LeaderCategory("passingLeader", "PYDS")]
        PassYards = 1,

        [LeaderCategory("rushingLeader", "RYDS")]
        RushYards = 2,

        [LeaderCategory("receivingLeader", "RECYDS")]
        RecYards = 3,

        [LeaderCategory("passingYards", "YDS")]
        PassingYards = 4,

        [LeaderCategory("rushingYards", "YDS")]
        RushingYards = 5,

        [LeaderCategory("receivingYards", "YDS")]
        ReceivingYards = 6,

        [LeaderCategory("totalTackles", "TOT")]
        Tackles = 7,

        [LeaderCategory("sacks", "SACK")]
        Sacks = 8,

        [LeaderCategory("interceptions", "INT")]
        Interceptions = 9,

        [LeaderCategory("puntReturns", "PR")]
        PuntReturns = 10,

        [LeaderCategory("kickReturns", "KR")]
        KickReturns = 11,

        [LeaderCategory("punts", "P")]
        Punts = 12,

        [LeaderCategory("totalKickingPoints", "TP")]
        KickingPoints = 13,

        [LeaderCategory("fumbles", "F")]
        Fumbles = 14,

        [LeaderCategory("fumblesLost", "FL")]
        FumblesLost = 15,

        [LeaderCategory("fumblesRecovered", "CMP")]
        FumblesRecovered = 16,

        [LeaderCategory("espnRating", "ESPNRating")]
        EspnRating = 17,

        [LeaderCategory("passingTouchdowns", "TD")]
        PassTouchdowns = 18,

        [LeaderCategory("quarterbackRating", "RAT")]
        QbRating = 19,

        [LeaderCategory("rushingTouchdowns", "TD")]
        RushTouchdowns = 20,

        [LeaderCategory("receptions", "REC")]
        Receptions = 21,

        [LeaderCategory("receivingTouchdowns", "TD")]
        RecTouchdowns = 22
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class LeaderCategoryAttribute : Attribute
    {
        public string Name { get; }
        public string Abbreviation { get; }

        public LeaderCategoryAttribute(string name, string abbreviation)
        {
            Name = name;
            Abbreviation = abbreviation;
        }
    }

    public static class FootballLeaderCategoryExtensions
    {
        public static string GetName(this FootballLeaderCategory category)
        {
            return category.GetAttribute()?.Name ?? "Unknown";
        }

        public static string GetAbbreviation(this FootballLeaderCategory category)
        {
            return category.GetAttribute()?.Abbreviation ?? "UNK";
        }

        public static FootballLeaderCategory FromName(string name)
        {
            return Enum.GetValues(typeof(FootballLeaderCategory))
                .Cast<FootballLeaderCategory>()
                .FirstOrDefault(c => string.Equals(c.GetName(), name, StringComparison.OrdinalIgnoreCase));
        }

        public static FootballLeaderCategory FromAbbreviation(string abbreviation)
        {
            return Enum.GetValues(typeof(FootballLeaderCategory))
                .Cast<FootballLeaderCategory>()
                .FirstOrDefault(c => string.Equals(c.GetAbbreviation(), abbreviation, StringComparison.OrdinalIgnoreCase));
        }

        private static LeaderCategoryAttribute? GetAttribute(this FootballLeaderCategory category)
        {
            return typeof(FootballLeaderCategory)
                .GetField(category.ToString())
                ?.GetCustomAttributes(typeof(LeaderCategoryAttribute), false)
                .Cast<LeaderCategoryAttribute>()
                .FirstOrDefault();
        }
    }

}
