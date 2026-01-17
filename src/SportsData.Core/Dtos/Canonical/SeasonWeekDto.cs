using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class SeasonWeekDto
    {
        public Guid Id { get; init; }

        public Guid SeasonId { get; init; }

        public int WeekNumber { get; init; }

        public string? SeasonPhase { get; init; }

        public int SeasonYear { get; init; }

        public bool IsNonStandardWeek { get; init; }

        public bool IsPostSeason => SeasonPhase?.ToLower() == "postseason";
    }
}
