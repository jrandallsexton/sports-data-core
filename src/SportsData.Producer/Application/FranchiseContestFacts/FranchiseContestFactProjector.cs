using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.FranchiseContestFacts;

/// <summary>
/// One game, as loaded for projection. Every value is a raw fact; the
/// derivation rules all live in <see cref="FranchiseContestFactProjector"/>.
/// </summary>
public record ContestFactInput
{
    public Guid ContestId { get; init; }
    public int SeasonYear { get; init; }
    public int? Week { get; init; }
    public int? SeasonPhaseTypeCode { get; init; }

    public DateTime KickoffUtc { get; init; }
    public bool KickoffTimeKnown { get; init; }

    public Guid? VenueId { get; init; }

    /// <summary>IANA id from Venue.TimeZone.</summary>
    public string? VenueTimeZone { get; init; }

    public bool IsNeutralSite { get; init; }

    /// <summary>ESPN's conferenceCompetition flag on the competition.</summary>
    public bool EspnConferenceFlag { get; init; }

    /// <summary>
    /// Whether the season publishes the flag (at least 10% of its games carry
    /// it), the same switch the conference record uses.
    /// </summary>
    public bool SeasonUsesConferenceFlag { get; init; }

    /// <summary>A competition note mentions "championship".</summary>
    public bool IsChampionship { get; init; }

    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }
    public Guid? WinnerFranchiseSeasonId { get; init; }
    public Guid? SpreadWinnerFranchiseSeasonId { get; init; }

    /// <summary>Home-relative line (negative = home favored), from the primary odds row.</summary>
    public decimal? HomeSpread { get; init; }
    public decimal? OverUnder { get; init; }
    public OverUnderResult TotalResult { get; init; }

    public required TeamFactInput Home { get; init; }
    public required TeamFactInput Away { get; init; }
}

public record TeamFactInput
{
    public Guid FranchiseSeasonId { get; init; }
    public Guid FranchiseId { get; init; }

    /// <summary>e.g. "NCAAF|NCAA|fbs|sec".</summary>
    public string? GroupSeasonMap { get; init; }

    /// <summary>
    /// The team's conference group: its GroupSeason, or that group's parent
    /// when the group is a division of a conference.
    /// </summary>
    public Guid? ConferenceGroupId { get; init; }

    public int? CuratedRank { get; init; }

    public int WinsBefore { get; init; }
    public int LossesBefore { get; init; }
    public int TiesBefore { get; init; }

    /// <summary>Null when the team has no box score for the game.</summary>
    public TeamBoxInput? Box { get; init; }
}

public record TeamBoxInput
{
    public int? TotalYards { get; init; }
    public int? PassingYards { get; init; }
    public int? RushingYards { get; init; }
    public int? PassAttempts { get; init; }
    public int? Completions { get; init; }
    public int? RushAttempts { get; init; }
    public int? TotalPlays { get; init; }
    public int? FirstDowns { get; init; }
    public int? ThirdDownConversions { get; init; }
    public int? ThirdDownAttempts { get; init; }
    public int? FourthDownConversions { get; init; }
    public int? FourthDownAttempts { get; init; }
    public int? RedZoneAttempts { get; init; }
    public int? RedZoneScores { get; init; }
    public int? Giveaways { get; init; }
    public int? Takeaways { get; init; }
    public int? Penalties { get; init; }
    public int? PenaltyYards { get; init; }
    public int? PossessionSeconds { get; init; }
    public int? SacksMade { get; init; }
    public int? SacksAllowed { get; init; }
}

/// <summary>
/// Derives the two <see cref="FranchiseContestFact"/> rows (home, away) for
/// one game. Pure: no data access, so every rule is unit tested here.
/// </summary>
public static class FranchiseContestFactProjector
{
    private const int PostseasonPhaseTypeCode = 3;
    private const int AfternoonStartHour = 14;
    private const int NightStartHour = 18;

    public static IReadOnlyList<FranchiseContestFact> Project(ContestFactInput input, DateTime projectedUtc)
    {
        var local = ToLocal(input);

        // Same rule as the conference record (EnrichFranchiseSeasonHandler):
        // ESPN's flag where the season publishes it; otherwise the two teams
        // share a conference, outside the postseason and championship games.
        var isConferenceGame = input.SeasonUsesConferenceFlag
            ? input.EspnConferenceFlag
            : input.Home.ConferenceGroupId is not null &&
              input.Home.ConferenceGroupId == input.Away.ConferenceGroupId &&
              input.SeasonPhaseTypeCode != PostseasonPhaseTypeCode &&
              !input.IsChampionship;

        return
        [
            ProjectSide(input, input.Home, input.Away, isHome: true, local, isConferenceGame, projectedUtc),
            ProjectSide(input, input.Away, input.Home, isHome: false, local, isConferenceGame, projectedUtc),
        ];
    }

    private static FranchiseContestFact ProjectSide(
        ContestFactInput input,
        TeamFactInput team,
        TeamFactInput opponent,
        bool isHome,
        LocalKickoff local,
        bool isConferenceGame,
        DateTime projectedUtc)
    {
        var pointsFor = isHome ? input.HomeScore : input.AwayScore;
        var pointsAgainst = isHome ? input.AwayScore : input.HomeScore;
        var margin = pointsFor - pointsAgainst;

        // Home-relative line, flipped for the away side so it is always this
        // team's line (same convention as GetFranchiseAtsBucketInstances.sql).
        var spread = isHome ? input.HomeSpread : -input.HomeSpread;

        var (division, conference) = ParseGroupSeasonMap(team.GroupSeasonMap);
        var (opponentDivision, opponentConference) = ParseGroupSeasonMap(opponent.GroupSeasonMap);

        var fact = new FranchiseContestFact
        {
            ContestId = input.ContestId,
            FranchiseSeasonId = team.FranchiseSeasonId,
            FranchiseId = team.FranchiseId,
            OpponentFranchiseSeasonId = opponent.FranchiseSeasonId,
            OpponentFranchiseId = opponent.FranchiseId,
            SeasonYear = input.SeasonYear,
            Week = input.Week,
            SeasonPhaseTypeCode = input.SeasonPhaseTypeCode,

            KickoffUtc = input.KickoffUtc,
            KickoffTimeKnown = input.KickoffTimeKnown,
            KickoffLocal = local.Kickoff,
            KickoffWindow = local.Kickoff is { } k ? ToWindow(k) : null,
            LocalMonth = local.Date?.Month,
            LocalDayOfWeek = local.Date is { } d ? (int)d.DayOfWeek : null,

            Site = input.IsNeutralSite ? GameSite.Neutral : isHome ? GameSite.Home : GameSite.Away,
            VenueId = input.VenueId,

            Conference = conference,
            OpponentConference = opponentConference,
            IsConferenceGame = isConferenceGame,
            Division = division,
            OpponentDivision = opponentDivision,

            RankGoingIn = team.CuratedRank,
            OpponentRankGoingIn = opponent.CuratedRank,

            WinsBefore = team.WinsBefore,
            LossesBefore = team.LossesBefore,
            TiesBefore = team.TiesBefore,
            OpponentWinsBefore = opponent.WinsBefore,
            OpponentLossesBefore = opponent.LossesBefore,
            OpponentTiesBefore = opponent.TiesBefore,

            PointsFor = pointsFor,
            PointsAgainst = pointsAgainst,
            Margin = margin,
            Result = ToResult(input, team, pointsFor, pointsAgainst),

            Spread = spread,
            OverUnder = input.OverUnder,
            AtsResult = ToAtsResult(input, team, spread, margin),
            TotalResult = input.TotalResult == OverUnderResult.None ? null : input.TotalResult,

            ProjectionVersion = FranchiseContestFact.CurrentProjectionVersion,
            ProjectedUtc = projectedUtc,
        };

        if (team.Box is { } box)
        {
            fact.TotalYards = box.TotalYards;
            fact.PassingYards = box.PassingYards;
            fact.RushingYards = box.RushingYards;
            fact.PassAttempts = box.PassAttempts;
            fact.Completions = box.Completions;
            fact.RushAttempts = box.RushAttempts;
            fact.TotalPlays = box.TotalPlays;
            fact.FirstDowns = box.FirstDowns;
            fact.ThirdDownConversions = box.ThirdDownConversions;
            fact.ThirdDownAttempts = box.ThirdDownAttempts;
            fact.FourthDownConversions = box.FourthDownConversions;
            fact.FourthDownAttempts = box.FourthDownAttempts;
            fact.RedZoneAttempts = box.RedZoneAttempts;
            fact.RedZoneScores = box.RedZoneScores;
            fact.Giveaways = box.Giveaways;
            fact.Takeaways = box.Takeaways;
            fact.Penalties = box.Penalties;
            fact.PenaltyYards = box.PenaltyYards;
            fact.PossessionSeconds = box.PossessionSeconds;
            fact.SacksMade = box.SacksMade;
            fact.SacksAllowed = box.SacksAllowed;
        }

        return fact;
    }

    private record LocalKickoff(DateTime? Kickoff, DateTime? Date);

    /// <summary>
    /// Local kickoff from the venue zone. A month or weekday must be the
    /// LOCAL date (an 8pm ET kickoff on Nov 30 is Dec 1 UTC). A date-only
    /// kickoff has no wall-clock time, and converting its placeholder time
    /// can cross midnight (07:00Z is 21:00 the day before in Honolulu), so
    /// its local date is the date ESPN published. No zone: nothing local.
    /// </summary>
    private static LocalKickoff ToLocal(ContestFactInput input)
    {
        if (string.IsNullOrWhiteSpace(input.VenueTimeZone) ||
            !TimeZoneInfo.TryFindSystemTimeZoneById(input.VenueTimeZone, out var zone))
        {
            return new LocalKickoff(null, null);
        }

        if (!input.KickoffTimeKnown)
        {
            return new LocalKickoff(null, input.KickoffUtc.Date);
        }

        var utc = DateTime.SpecifyKind(input.KickoffUtc, DateTimeKind.Utc);
        var local = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, zone), DateTimeKind.Unspecified);
        return new LocalKickoff(local, local.Date);
    }

    private static KickoffWindow ToWindow(DateTime localKickoff) =>
        localKickoff.Hour >= NightStartHour ? Enums.KickoffWindow.Night
        : localKickoff.Hour >= AfternoonStartHour ? Enums.KickoffWindow.Afternoon
        : Enums.KickoffWindow.Noon;

    /// <summary>
    /// The contest's winner, as the W/L record counts it; a tie is a
    /// finalized game with scores and no winner. No scores: no result.
    /// </summary>
    private static GameResult? ToResult(ContestFactInput input, TeamFactInput team, int? pointsFor, int? pointsAgainst)
    {
        if (pointsFor is null || pointsAgainst is null)
            return null;

        if (input.WinnerFranchiseSeasonId is null)
            return GameResult.Tie;

        return input.WinnerFranchiseSeasonId == team.FranchiseSeasonId ? GameResult.Win : GameResult.Loss;
    }

    /// <summary>
    /// The contest's spread winner decides cover / no cover (the value every
    /// ATS surface reads). It is null for a push or an unsourced result, so a
    /// push is only claimed when the line and the margin show one.
    /// </summary>
    private static AtsResult? ToAtsResult(ContestFactInput input, TeamFactInput team, decimal? spread, int? margin)
    {
        if (input.SpreadWinnerFranchiseSeasonId is { } winner)
            return winner == team.FranchiseSeasonId ? Enums.AtsResult.Covered : Enums.AtsResult.NotCovered;

        if (spread is not null && margin is not null && margin.Value + spread.Value == 0)
            return Enums.AtsResult.Push;

        return null;
    }

    /// <summary>
    /// "NCAAF|NCAA|fbs|sec" → ("fbs", "sec"); "NCAAF|yy" → (null, null);
    /// "NFL|NFC|NORTH" → ("NFC NORTH", "NFC").
    /// </summary>
    private static (string? Division, string? Conference) ParseGroupSeasonMap(string? map)
    {
        if (string.IsNullOrWhiteSpace(map))
            return (null, null);

        var parts = map.Split('|', StringSplitOptions.TrimEntries);

        // NFL: "NFL|NFC|NORTH" is conference then division; the division
        // alone ("NORTH") is ambiguous across conferences, so it carries both.
        if (parts[0] == "NFL")
        {
            var nflConference = parts.Length >= 2 && parts[1].Length > 0 ? parts[1] : null;
            var nflDivision = nflConference is not null && parts.Length >= 3 && parts[2].Length > 0
                ? $"{nflConference} {parts[2]}"
                : null;
            return (nflDivision, nflConference);
        }

        var division = parts.Length >= 3 && parts[2].Length > 0 ? parts[2] : null;
        var conference = parts.Length >= 4 && parts[3].Length > 0 ? parts[3] : null;
        return (division, conference);
    }
}
