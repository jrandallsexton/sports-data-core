using SportsData.Core.Common;

using System;

namespace SportsData.Core.Extensions;

public static class DocumentTypeExtensions
{
    public static string ToKebabCase(this DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Athlete => "athlete",
            DocumentType.AthleteImage => "athlete-image",
            DocumentType.AthletePosition => "athlete-position",
            DocumentType.AthleteSeason => "athlete-season",
            DocumentType.Award => "award",
            DocumentType.Coach => "coach",
            DocumentType.CoachSeason => "coach-season",
            DocumentType.Contest => "contest",
            DocumentType.Event => "event",
            DocumentType.EventCompetition => "event-competition",
            DocumentType.EventCompetitionBroadcast => "event-competition-broadcast",
            DocumentType.EventCompetitionCompetitor => "event-competition-competitor",
            DocumentType.EventCompetitionCompetitorLineScore => "event-competition-competitor-line-score",
            DocumentType.EventCompetitionCompetitorScore => "event-competition-competitor-score",
            DocumentType.EventCompetitionDrive => "event-competition-drive",
            DocumentType.EventCompetitionLeaders => "event-competition-leaders",
            DocumentType.EventCompetitionOdds => "event-competition-odds",
            DocumentType.EventCompetitionPlay => "event-competition-play",
            DocumentType.EventCompetitionPowerIndex => "event-competition-power-index",
            DocumentType.EventCompetitionPrediction => "event-competition-prediction",
            DocumentType.EventCompetitionProbability => "event-competition-probability",
            DocumentType.EventCompetitionStatus => "event-competition-status",
            DocumentType.Franchise => "franchise",
            DocumentType.FranchiseLogo => "franchise-logo",
            DocumentType.GameSummary => "game-summary",
            DocumentType.GolfCalendar => "golf-calendar",
            DocumentType.GroupLogo => "group-logo",
            DocumentType.GroupSeason => "group-season",
            DocumentType.GroupSeasonLogo => "group-season-logo",
            DocumentType.Position => "position",
            DocumentType.Scoreboard => "scoreboard",
            DocumentType.Season => "season",
            DocumentType.SeasonFuture => "season-future",
            DocumentType.SeasonType => "season-type",
            DocumentType.SeasonTypeWeek => "season-type-week",
            DocumentType.SeasonTypeWeekRankings => "season-type-week-rankings",
            DocumentType.Seasons => "seasons",
            DocumentType.Standings => "standings",
            DocumentType.FranchiseSeasonLogo => "franchise-season-logo",
            DocumentType.TeamSeason => "team-season",
            DocumentType.TeamSeasonAward => "team-season-award",
            DocumentType.TeamSeasonInjuries => "team-season-injuries",
            DocumentType.TeamSeasonLeaders => "team-season-leaders",
            DocumentType.TeamSeasonProjection => "team-season-projection",
            DocumentType.TeamSeasonRank => "team-season-rank",
            DocumentType.TeamSeasonRecord => "team-season-record",
            DocumentType.TeamSeasonRecordAts => "team-season-record-ats",
            DocumentType.TeamSeasonStatistics => "team-season-statistics",
            DocumentType.Venue => "venue",
            DocumentType.VenueImage => "venue-image",
            DocumentType.Weeks => "weeks",
            DocumentType.CoachRecord => "coach-record",
            DocumentType.SeasonRanking => "season-ranking",
            DocumentType.EventCompetitionSituation => "event-competition-situation",
            DocumentType.SeasonPoll => "season-poll",
            DocumentType.SeasonPollWeek => "season-poll-week",
            DocumentType.EventCompetitionAthleteStatistics => "event-competition-athlete-statistics",
            DocumentType.EventCompetitionCompetitorStatistics => "event-competition-competitor-statistics",
            DocumentType.Unknown => "unknown",
            _ => throw new ArgumentOutOfRangeException(nameof(documentType), documentType, "Unsupported DocumentType")
        };
    }
}