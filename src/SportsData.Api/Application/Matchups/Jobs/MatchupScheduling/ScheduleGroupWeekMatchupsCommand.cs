namespace SportsData.Api.Application.Matchups.Jobs.MatchupScheduling;

public record ScheduleGroupWeekMatchupsCommand(
    Guid GroupId,
    Guid SeasonWeekId,
    int SeasonYear,
    int SeasonWeek,
    bool IsNonStandardWeek,
    Guid CorrelationId,
    bool IsRefresh = false);