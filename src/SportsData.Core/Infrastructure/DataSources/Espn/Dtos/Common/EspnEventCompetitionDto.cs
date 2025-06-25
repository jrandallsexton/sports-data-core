#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

/// <summary>
/// Represents a competition within an ESPN event, providing detailed information about the competition's attributes, 
/// participants, status, and related resources.
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334
/// </summary>
/// <remarks>This class encapsulates various properties that describe the competition, including its unique
/// identifiers,  date, venue, competitors, and availability of features such as play-by-play, boxscore, and commentary.
/// It also includes links to related resources such as broadcasts, odds, and leaders.</remarks>
public class EspnEventCompetitionDto : IHasRef
{
    [JsonPropertyName("$ref")]
    public Uri Ref { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("guid")]
    public string Guid { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }

    [JsonPropertyName("date")]
    public string Date { get; set; }

    [JsonPropertyName("attendance")]
    public int Attendance { get; set; }

    [JsonPropertyName("type")]
    public EspnEventCompetitionTypeDto Type { get; set; }

    [JsonPropertyName("timeValid")]
    public bool TimeValid { get; set; }

    [JsonPropertyName("dateValid")]
    public bool DateValid { get; set; }

    [JsonPropertyName("neutralSite")]
    public bool NeutralSite { get; set; }

    [JsonPropertyName("divisionCompetition")]
    public bool DivisionCompetition { get; set; }

    [JsonPropertyName("conferenceCompetition")]
    public bool ConferenceCompetition { get; set; }

    [JsonPropertyName("previewAvailable")]
    public bool PreviewAvailable { get; set; }

    [JsonPropertyName("recapAvailable")]
    public bool RecapAvailable { get; set; }

    [JsonPropertyName("boxscoreAvailable")]
    public bool BoxscoreAvailable { get; set; }

    [JsonPropertyName("lineupAvailable")]
    public bool LineupAvailable { get; set; }

    [JsonPropertyName("gamecastAvailable")]
    public bool GamecastAvailable { get; set; }

    [JsonPropertyName("playByPlayAvailable")]
    public bool PlayByPlayAvailable { get; set; }

    [JsonPropertyName("conversationAvailable")]
    public bool ConversationAvailable { get; set; }

    [JsonPropertyName("commentaryAvailable")]
    public bool CommentaryAvailable { get; set; }

    [JsonPropertyName("pickcenterAvailable")]
    public bool PickcenterAvailable { get; set; }

    [JsonPropertyName("summaryAvailable")]
    public bool SummaryAvailable { get; set; }

    [JsonPropertyName("liveAvailable")]
    public bool LiveAvailable { get; set; }

    [JsonPropertyName("ticketsAvailable")]
    public bool TicketsAvailable { get; set; }

    [JsonPropertyName("shotChartAvailable")]
    public bool ShotChartAvailable { get; set; }

    [JsonPropertyName("timeoutsAvailable")]
    public bool TimeoutsAvailable { get; set; }

    [JsonPropertyName("possessionArrowAvailable")]
    public bool PossessionArrowAvailable { get; set; }

    [JsonPropertyName("onWatchESPN")]
    public bool OnWatchESPN { get; set; }

    [JsonPropertyName("recent")]
    public bool Recent { get; set; }

    [JsonPropertyName("bracketAvailable")]
    public bool BracketAvailable { get; set; }

    [JsonPropertyName("wallclockAvailable")]
    public bool WallclockAvailable { get; set; }

    [JsonPropertyName("highlightsAvailable")]
    public bool HighlightsAvailable { get; set; }

    [JsonPropertyName("gameSource")]
    public EspnEventCompetitionGameSourceDto GameSource { get; set; }

    [JsonPropertyName("boxscoreSource")]
    public EspnEventCompetitionBoxscoreSourceDto BoxscoreSource { get; set; }

    [JsonPropertyName("playByPlaySource")]
    public EspnEventCompetitionPlayByPlaySourceDto PlayByPlaySource { get; set; }

    [JsonPropertyName("linescoreSource")]
    public EspnEventCompetitionLinescoreSourceDto LinescoreSource { get; set; }

    [JsonPropertyName("statsSource")]
    public EspnEventCompetitionStatsSourceDto StatsSource { get; set; }

    [JsonPropertyName("venue")]
    public EspnVenueDto Venue { get; set; }

    [JsonPropertyName("competitors")]
    public List<EspnEventCompetitionCompetitorDto> Competitors { get; set; }

    [JsonPropertyName("notes")]
    public List<EspnEventCompetitionNoteDto> Notes { get; set; }

    [JsonPropertyName("situation")]
    public EspnLinkDto Situation { get; set; }

    [JsonPropertyName("status")]
    public EspnLinkDto Status { get; set; }

    [JsonPropertyName("odds")]
    public EspnLinkDto Odds { get; set; }

    [JsonPropertyName("broadcasts")]
    public EspnLinkDto Broadcasts { get; set; }

    [JsonPropertyName("details")]
    public EspnLinkDto Details { get; set; }

    [JsonPropertyName("leaders")]
    public EspnLinkDto Leaders { get; set; }

    [JsonPropertyName("links")]
    public List<EspnLinkFullDto> Links { get; set; }

    [JsonPropertyName("predictor")]
    public EspnLinkDto Predictor { get; set; }

    [JsonPropertyName("probabilities")]
    public EspnLinkDto Probabilities { get; set; }

    [JsonPropertyName("powerIndexes")]
    public EspnLinkDto PowerIndexes { get; set; }

    [JsonPropertyName("format")]
    public EspnEventCompetitionFormatDto Format { get; set; }

    [JsonPropertyName("drives")]
    public EspnLinkDto Drives { get; set; }

    [JsonPropertyName("hasDefensiveStats")]
    public bool HasDefensiveStats { get; set; }
}