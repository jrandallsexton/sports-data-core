namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetRankingsByPollByWeek;

public record GetRankingsByPollByWeekQuery(string PollType, int SeasonYear, int WeekNumber);
