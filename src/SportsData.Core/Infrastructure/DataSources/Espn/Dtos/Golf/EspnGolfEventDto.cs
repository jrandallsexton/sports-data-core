using Newtonsoft.Json;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf
{
    public class EspnGolfEventDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public string Guid { get; set; }

        public string Uid { get; set; }

        public EspnAlternateIdDto AlternateIds { get; set; }

        public string Date { get; set; }

        public string EndDate { get; set; }

        public string Name { get; set; }

        public string ShortName { get; set; }

        public EspnLinkDto Season { get; set; }

        public bool TimeValid { get; set; }

        public List<Competition> Competitions { get; set; }

        public List<EspnLinkFullDto> Links { get; set; }

        public List<EspnLinkDto> Venues { get; set; }

        public EspnLinkDto League { get; set; }

        public DefendingChampion DefendingChampion { get; set; }

        public EspnLinkDto Tournament { get; set; }

        public Status Status { get; set; }

        public double Purse { get; set; }

        public string DisplayPurse { get; set; }

        public PlayoffType PlayoffType { get; set; }

        public Winner Winner { get; set; }

        public List<Course> Courses { get; set; }

        public bool Primary { get; set; }

        public bool HasPlayerStats { get; set; }

        public bool HasCourseStats { get; set; }

        public bool IsCupPlayoff { get; set; }

        public bool IsSignature { get; set; }
    }

    public class EspnGolfEventAthleteDto
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public string Uid { get; set; }

        public string Guid { get; set; }

        public EspnAlternateIdDto AlternateIds { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string FullName { get; set; }

        public string DisplayName { get; set; }

        public string ShortName { get; set; }

        public double Height { get; set; }

        public string DisplayHeight { get; set; }

        public int Age { get; set; }

        public string DateOfBirth { get; set; }

        public bool Amateur { get; set; }

        public List<EspnLinkFullDto> Links { get; set; }

        public BirthPlace BirthPlace { get; set; }

        public string Citizenship { get; set; }

        public CitizenshipCountry CitizenshipCountry { get; set; }

        public EspnLinkDto College { get; set; }

        public EspnImageDto Headshot { get; set; }

        public Hand Hand { get; set; }

        public EspnImageDto Flag { get; set; }

        public bool Linked { get; set; }

        public bool Active { get; set; }

        public Status Status { get; set; }

        public EspnLinkDto Statisticslog { get; set; }

        public EspnLinkDto Seasons { get; set; }

        public double Weight { get; set; }

        public string DisplayWeight { get; set; }

        public int DebutYear { get; set; }

        public EspnLinkDto Statistics { get; set; }

        public Experience Experience { get; set; }

        public EspnLinkDto EventLog { get; set; }
    }

    public class BirthPlace
    {
        public string City { get; set; }
        public string State { get; set; }
        public string StateAbbreviation { get; set; }
        public string Country { get; set; }
        public string CountryAbbreviation { get; set; }
    }

    public class BoxscoreSource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class CitizenshipCountry
    {
        public string Name { get; set; }
        public string Abbreviation { get; set; }
    }

    public class Competition
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public string Uid { get; set; }

        public string Date { get; set; }

        public string EndDate { get; set; }

        public ScoringSystem ScoringSystem { get; set; }

        public bool Necessary { get; set; }

        public bool TimeValid { get; set; }

        public bool NeutralSite { get; set; }

        public bool DivisionCompetition { get; set; }

        public bool ConferenceCompetition { get; set; }

        public bool PreviewAvailable { get; set; }

        public bool RecapAvailable { get; set; }

        public bool BoxscoreAvailable { get; set; }

        public bool LineupAvailable { get; set; }

        public bool GamecastAvailable { get; set; }

        public bool PlayByPlayAvailable { get; set; }

        public bool ConversationAvailable { get; set; }

        public bool CommentaryAvailable { get; set; }

        public bool PickcenterAvailable { get; set; }

        public bool SummaryAvailable { get; set; }

        public bool LiveAvailable { get; set; }

        public bool TicketsAvailable { get; set; }

        public bool ShotChartAvailable { get; set; }

        public bool TimeoutsAvailable { get; set; }

        public bool PossessionArrowAvailable { get; set; }

        public bool OnWatchEspn { get; set; }

        public bool Recent { get; set; }

        public bool BracketAvailable { get; set; }

        public bool WallclockAvailable { get; set; }

        public GameSource GameSource { get; set; }

        public BoxscoreSource BoxscoreSource { get; set; }

        public PlayByPlaySource PlayByPlaySource { get; set; }

        public LinescoreSource LinescoreSource { get; set; }

        public StatsSource StatsSource { get; set; }

        public List<Competitor> Competitors { get; set; }

        public Status Status { get; set; }

        public EspnLinkDto Broadcasts { get; set; }

        public EspnLinkDto Leaders { get; set; }

        public List<EspnLinkFullDto> Links { get; set; }

        public string DataFormat { get; set; }

        public HoleByHoleSource HoleByHoleSource { get; set; }
    }

    public class Competitor
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }

        public string Id { get; set; }

        public string Uid { get; set; }

        public string Type { get; set; }

        public int Order { get; set; }

        public EspnGolfEventAthleteDto AthleteDto { get; set; }

        public Status Status { get; set; }

        public EspnLinkDto Score { get; set; }

        public EspnLinkDto Linescores { get; set; }

        public EspnLinkDto Statistics { get; set; }

        public int Movement { get; set; }

        public double Earnings { get; set; }

        public bool Amateur { get; set; }
    }

    public class Course
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public EspnAddressDto Address { get; set; }

        public EspnLinkDto Venue { get; set; }

        public int TotalYards { get; set; }

        public int ShotsToPar { get; set; }

        public int ParIn { get; set; }

        public int ParOut { get; set; }

        public List<Hole> Holes { get; set; }

        public EspnLinkDto TournamentOverallStats { get; set; }

        public List<EspnLinkDto> TournamentRoundStats { get; set; }

        public bool Host { get; set; }
    }

    public class DefendingChampion
    {
        public EspnGolfEventAthleteDto AthleteDto { get; set; }
    }

    public class Experience
    {
        public int Years { get; set; }
    }

    public class GameSource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class Hand
    {
        public string Type { get; set; }
        public string Abbreviation { get; set; }
        public string DisplayValue { get; set; }
    }

    public class Hole
    {
        public int Number { get; set; }
        public int ShotsToPar { get; set; }
        public int TotalYards { get; set; }
    }

    public class HoleByHoleSource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class LinescoreSource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class PlayByPlaySource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class PlayoffType
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public int MinimumHoles { get; set; }
    }

    public class ScoringSystem
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public class StatsSource
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string State { get; set; }
    }

    public class Status
    {
        [JsonProperty("$ref")]
        public string Ref { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public StatusType Type { get; set; }
        public string Abbreviation { get; set; }
    }

    public class StatusType
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public bool Completed { get; set; }
        public string Description { get; set; }
    }

    public class Winner
    {
        public EspnGolfEventAthleteDto AthleteDto { get; set; }
    }
}
