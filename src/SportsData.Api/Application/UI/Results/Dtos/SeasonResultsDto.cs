using System;
using System.Collections.Generic;

namespace SportsData.Api.Application.UI.Results.Dtos;

public class SeasonResultsDto
{
    public string Sport { get; set; } = default!;
    public string League { get; set; } = default!;
    public int SeasonYear { get; set; }

    public AggregateRecordDto Aggregate { get; set; } = new();

    public List<WeekResultsDto> Weeks { get; set; } = new();
}

public class AggregateRecordDto
{
    public int SuWins { get; set; }
    public int SuLosses { get; set; }
    public int AtsWins { get; set; }
    public int AtsLosses { get; set; }
    public int AtsPushes { get; set; }
    public int TotalGames { get; set; }
}

public class WeekResultsDto
{
    public int WeekNumber { get; set; }
    public DateTime SeasonWeekEndDate { get; set; }

    public AggregateRecordDto Aggregate { get; set; } = new();

    public List<GameResultTileDto> Games { get; set; } = new();
}

public class GameResultTileDto
{
    public Guid ContestId { get; set; }
    public DateTime StartDateUtc { get; set; }

    // Away team
    public string Away { get; set; } = default!;
    public string AwayShort { get; set; } = default!;
    public string? AwayLogoUri { get; set; }
    public Guid AwayFranchiseSeasonId { get; set; }
    public int? AwayScore { get; set; }

    // Home team
    public string Home { get; set; } = default!;
    public string HomeShort { get; set; } = default!;
    public string? HomeLogoUri { get; set; }
    public Guid HomeFranchiseSeasonId { get; set; }
    public int? HomeScore { get; set; }

    // Spread (at preview time would be ideal — for MVP we surface the
    // current/closing line from the canonical matchup)
    public double? Spread { get; set; }

    // Our picks
    public Guid? PredictedStraightUpWinner { get; set; }
    public Guid? PredictedSpreadWinner { get; set; }

    // Actual outcome
    public Guid? ActualStraightUpWinner { get; set; }
    public Guid? ActualSpreadWinner { get; set; }

    // Computed result.
    //   null  = cannot evaluate (missing pick or missing actual)
    //   true  = hit
    //   false = miss
    public bool? SuHit { get; set; }
    public bool? AtsHit { get; set; }
    public bool AtsPush { get; set; }
}
