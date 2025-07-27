#pragma warning disable CS8618 // Non-nullable property is uninitialized

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf
{
    public class BirthPlace
    {
        [JsonPropertyName("city")]
        public string City { get; set; }

        [JsonPropertyName("country")]
        public string Country { get; set; }

        [JsonPropertyName("countryAbbreviation")]
        public string CountryAbbreviation { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("stateAbbreviation")]
        public string StateAbbreviation { get; set; }
    }

    public class BoxscoreSource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class CitizenshipCountry
    {
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class EspnGolfEventCompetition
    {
        [JsonPropertyName("boxscoreAvailable")]
        public bool BoxscoreAvailable { get; set; }

        [JsonPropertyName("boxscoreSource")]
        public BoxscoreSource BoxscoreSource { get; set; }

        [JsonPropertyName("bracketAvailable")]
        public bool BracketAvailable { get; set; }

        [JsonPropertyName("broadcasts")]
        public EspnLinkDto Broadcasts { get; set; }

        [JsonPropertyName("commentaryAvailable")]
        public bool CommentaryAvailable { get; set; }

        [JsonPropertyName("competitors")]
        public List<EspnGolfEventCompetitor> Competitors { get; set; }

        [JsonPropertyName("conferenceCompetition")]
        public bool ConferenceCompetition { get; set; }

        [JsonPropertyName("conversationAvailable")]
        public bool ConversationAvailable { get; set; }

        [JsonPropertyName("dataFormat")]
        public string DataFormat { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("divisionCompetition")]
        public bool DivisionCompetition { get; set; }

        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        [JsonPropertyName("gamecastAvailable")]
        public bool GamecastAvailable { get; set; }

        [JsonPropertyName("gameSource")]
        public GameSource GameSource { get; set; }

        [JsonPropertyName("holeByHoleSource")]
        public HoleByHoleSource HoleByHoleSource { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("leaders")]
        public EspnLinkDto Leaders { get; set; }

        [JsonPropertyName("linescoreSource")]
        public LinescoreSource LinescoreSource { get; set; }

        [JsonPropertyName("lineupAvailable")]
        public bool LineupAvailable { get; set; }

        [JsonPropertyName("links")]
        public List<EspnLinkFullDto> Links { get; set; }

        [JsonPropertyName("liveAvailable")]
        public bool LiveAvailable { get; set; }

        [JsonPropertyName("necessary")]
        public bool Necessary { get; set; }

        [JsonPropertyName("neutralSite")]
        public bool NeutralSite { get; set; }

        [JsonPropertyName("onWatchEspn")]
        public bool OnWatchEspn { get; set; }

        [JsonPropertyName("pickcenterAvailable")]
        public bool PickcenterAvailable { get; set; }

        [JsonPropertyName("playByPlayAvailable")]
        public bool PlayByPlayAvailable { get; set; }

        [JsonPropertyName("playByPlaySource")]
        public PlayByPlaySource PlayByPlaySource { get; set; }

        [JsonPropertyName("possessionArrowAvailable")]
        public bool PossessionArrowAvailable { get; set; }

        [JsonPropertyName("previewAvailable")]
        public bool PreviewAvailable { get; set; }

        [JsonPropertyName("recapAvailable")]
        public bool RecapAvailable { get; set; }

        [JsonPropertyName("recent")]
        public bool Recent { get; set; }

        [JsonPropertyName("scoringSystem")]
        public ScoringSystem ScoringSystem { get; set; }

        [JsonPropertyName("shotChartAvailable")]
        public bool ShotChartAvailable { get; set; }

        [JsonPropertyName("statsSource")]
        public StatsSource StatsSource { get; set; }

        [JsonPropertyName("status")]
        public Status Status { get; set; }

        [JsonPropertyName("summaryAvailable")]
        public bool SummaryAvailable { get; set; }

        [JsonPropertyName("ticketsAvailable")]
        public bool TicketsAvailable { get; set; }

        [JsonPropertyName("timeoutsAvailable")]
        public bool TimeoutsAvailable { get; set; }

        [JsonPropertyName("timeValid")]
        public bool TimeValid { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }
        [JsonPropertyName("wallclockAvailable")]
        public bool WallclockAvailable { get; set; }
    }

    public class EspnGolfEventCompetitor
    {
        [JsonPropertyName("amateur")]
        public bool Amateur { get; set; }

        [JsonPropertyName("athleteDto")]
        public EspnGolfEventAthleteDto AthleteDto { get; set; }

        [JsonPropertyName("earnings")]
        public double Earnings { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("linescores")]
        public EspnLinkDto Linescores { get; set; }

        [JsonPropertyName("movement")]
        public int Movement { get; set; }

        [JsonPropertyName("order")]
        public int Order { get; set; }

        [JsonPropertyName("score")]
        public EspnLinkDto Score { get; set; }

        [JsonPropertyName("statistics")]
        public EspnLinkDto Statistics { get; set; }

        [JsonPropertyName("status")]
        public Status Status { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("uid")]
        public string Uid { get; set; }
    }

    public class Course
    {
        [JsonPropertyName("address")]
        public EspnAddressDto Address { get; set; }

        [JsonPropertyName("holes")]
        public List<Hole> Holes { get; set; }

        [JsonPropertyName("host")]
        public bool Host { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("parIn")]
        public int ParIn { get; set; }

        [JsonPropertyName("parOut")]
        public int ParOut { get; set; }

        [JsonPropertyName("shotsToPar")]
        public int ShotsToPar { get; set; }

        [JsonPropertyName("totalYards")]
        public int TotalYards { get; set; }

        [JsonPropertyName("tournamentOverallStats")]
        public EspnLinkDto TournamentOverallStats { get; set; }

        [JsonPropertyName("tournamentRoundStats")]
        public List<EspnLinkDto> TournamentRoundStats { get; set; }

        [JsonPropertyName("venue")]
        public EspnLinkDto Venue { get; set; }
    }

    public class DefendingChampion
    {
        [JsonPropertyName("athleteDto")]
        public EspnGolfEventAthleteDto AthleteDto { get; set; }
    }

    public class EspnGolfEventAthleteDto
    {
        [JsonPropertyName("active")] public bool Active { get; set; }
        [JsonPropertyName("age")] public int Age { get; set; }
        [JsonPropertyName("alternateIds")] public EspnAlternateIdDto AlternateIds { get; set; }
        [JsonPropertyName("amateur")] public bool Amateur { get; set; }
        [JsonPropertyName("birthPlace")] public BirthPlace BirthPlace { get; set; }
        [JsonPropertyName("citizenship")] public string Citizenship { get; set; }
        [JsonPropertyName("citizenshipCountry")]
        public CitizenshipCountry CitizenshipCountry { get; set; }

        [JsonPropertyName("college")] public EspnLinkDto College { get; set; }
        [JsonPropertyName("dateOfBirth")] public string DateOfBirth { get; set; }
        [JsonPropertyName("debutYear")] public int DebutYear { get; set; }
        [JsonPropertyName("displayHeight")] public string DisplayHeight { get; set; }
        [JsonPropertyName("displayName")] public string DisplayName { get; set; }
        [JsonPropertyName("displayWeight")] public string DisplayWeight { get; set; }
        [JsonPropertyName("eventLog")] public EspnLinkDto EventLog { get; set; }
        [JsonPropertyName("experience")] public Experience Experience { get; set; }
        [JsonPropertyName("firstName")] public string FirstName { get; set; }
        [JsonPropertyName("flag")] public EspnImageDto Flag { get; set; }
        [JsonPropertyName("fullName")] public string FullName { get; set; }
        [JsonPropertyName("guid")] public string Guid { get; set; }
        [JsonPropertyName("hand")] public Hand Hand { get; set; }
        [JsonPropertyName("headshot")] public EspnImageDto Headshot { get; set; }
        [JsonPropertyName("height")] public double Height { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; }

        [JsonPropertyName("lastName")] public string LastName { get; set; }
        [JsonPropertyName("linked")] public bool Linked { get; set; }
        [JsonPropertyName("links")] public List<EspnLinkFullDto> Links { get; set; }
        [JsonPropertyName("seasons")] public EspnLinkDto Seasons { get; set; }
        [JsonPropertyName("shortName")] public string ShortName { get; set; }
        [JsonPropertyName("statistics")] public EspnLinkDto Statistics { get; set; }
        [JsonPropertyName("statisticslog")] public EspnLinkDto Statisticslog { get; set; }
        [JsonPropertyName("status")] public Status Status { get; set; }
        [JsonPropertyName("uid")] public string Uid { get; set; }
        [JsonPropertyName("weight")] public double Weight { get; set; }
    }

    public class EspnGolfEventDto
    {
        [JsonPropertyName("alternateIds")] public EspnAlternateIdDto AlternateIds { get; set; }
        [JsonPropertyName("competitions")] public List<EspnGolfEventCompetition> Competitions { get; set; }
        [JsonPropertyName("courses")] public List<Course> Courses { get; set; }
        [JsonPropertyName("date")] public string Date { get; set; }
        [JsonPropertyName("defendingChampion")]
        public DefendingChampion DefendingChampion { get; set; }

        [JsonPropertyName("displayPurse")] public string DisplayPurse { get; set; }
        [JsonPropertyName("endDate")] public string EndDate { get; set; }
        [JsonPropertyName("guid")] public string Guid { get; set; }
        [JsonPropertyName("hasCourseStats")] public bool HasCourseStats { get; set; }
        [JsonPropertyName("hasPlayerStats")] public bool HasPlayerStats { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; }
        [JsonPropertyName("isCupPlayoff")] public bool IsCupPlayoff { get; set; }
        [JsonPropertyName("isSignature")] public bool IsSignature { get; set; }
        [JsonPropertyName("league")] public EspnLinkDto League { get; set; }
        [JsonPropertyName("links")] public List<EspnLinkFullDto> Links { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("playoffType")] public PlayoffType PlayoffType { get; set; }
        [JsonPropertyName("primary")] public bool Primary { get; set; }
        [JsonPropertyName("purse")] public double Purse { get; set; }
        [JsonPropertyName("season")] public EspnLinkDto Season { get; set; }
        [JsonPropertyName("shortName")] public string ShortName { get; set; }
        [JsonPropertyName("status")] public Status Status { get; set; }
        [JsonPropertyName("timeValid")] public bool TimeValid { get; set; }
        [JsonPropertyName("tournament")] public EspnLinkDto Tournament { get; set; }
        [JsonPropertyName("uid")] public string Uid { get; set; }
        [JsonPropertyName("venues")] public List<EspnLinkDto> Venues { get; set; }
        [JsonPropertyName("winner")] public Winner Winner { get; set; }
    }
    public class Experience
    {
        [JsonPropertyName("years")]
        public int Years { get; set; }
    }

    public class GameSource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class Hand
    {
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("displayValue")]
        public string DisplayValue { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class Hole
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("shotsToPar")]
        public int ShotsToPar { get; set; }

        [JsonPropertyName("totalYards")]
        public int TotalYards { get; set; }
    }

    public class HoleByHoleSource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class LinescoreSource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class PlayByPlaySource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class PlayoffType
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("minimumHoles")]
        public int MinimumHoles { get; set; }
    }

    public class ScoringSystem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class StatsSource
    {
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class Status
    {
        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public StatusType Type { get; set; }
    }

    public class StatusType
    {
        [JsonPropertyName("completed")]
        public bool Completed { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }
    }

    public class Winner
    {
        [JsonPropertyName("athleteDto")]
        public EspnGolfEventAthleteDto AthleteDto { get; set; }
    }
    // ... other classes (Competition, Competitor, Course, etc.) follow the same structure ...

    // Let me know if you'd like the remaining classes (e.g., Competition, Course, Status, etc.) converted in full
    // or if you want a downloadable file or snippet batch for just those.
}