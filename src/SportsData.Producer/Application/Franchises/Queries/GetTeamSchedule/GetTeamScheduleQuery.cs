namespace SportsData.Producer.Application.Franchises.Queries.GetTeamSchedule;

public record GetTeamScheduleQuery(string Slug, int SeasonYear, DateTime? AsOfDate);
