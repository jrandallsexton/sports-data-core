#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Common.Routing;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football
{
    public class EspnFootballCalendarDto : EspnResourceIndexDto, IHasRoutingKey
    {
        public string RoutingKey { get; } = "espn.v2.sports.football.leagues.college-football.calendar";
    }
}