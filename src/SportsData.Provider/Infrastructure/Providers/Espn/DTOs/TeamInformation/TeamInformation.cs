namespace SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation
{
    public class Logo
    {
        public string href { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string alt { get; set; }
        public List<string> rel { get; set; }
    }

    public class Stat
    {
        public string name { get; set; }
        public double value { get; set; }
    }

    public class Item
    {
        public string description { get; set; }
        public string type { get; set; }
        public string summary { get; set; }
        public List<Stat> stats { get; set; }
    }

    public class Record
    {
        public List<Item> items { get; set; }
    }

    public class Parent
    {
        public string id { get; set; }
    }

    public class Groups
    {
        public string id { get; set; }
        public Parent parent { get; set; }
        public bool isConference { get; set; }
    }

    public class Link
    {
        public string language { get; set; }
        public List<string> rel { get; set; }
        public string href { get; set; }
        public string text { get; set; }
        public string shortText { get; set; }
        public bool isExternal { get; set; }
        public bool isPremium { get; set; }
    }

    public class Season
    {
        public int year { get; set; }
        public string displayName { get; set; }
    }

    public class SeasonType
    {
        public string id { get; set; }
        public int type { get; set; }
        public string name { get; set; }
        public string abbreviation { get; set; }
    }

    public class Week
    {
        public int number { get; set; }
        public string text { get; set; }
    }

    public class Type
    {
        public string id { get; set; }
        public string text { get; set; }
        public string abbreviation { get; set; }
    }

    public class Address
    {
        public string city { get; set; }
        public string state { get; set; }
        public string zipCode { get; set; }
    }

    public class Venue
    {
        public string fullName { get; set; }
        public Address address { get; set; }
    }

    public class Logo2
    {
        public string href { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string alt { get; set; }
        public List<string> rel { get; set; }
    }

    public class Link2
    {
        public List<string> rel { get; set; }
        public string href { get; set; }
        public string text { get; set; }
    }

    public class Team2
    {
        public string id { get; set; }
        public string location { get; set; }
        public string nickname { get; set; }
        public string abbreviation { get; set; }
        public string displayName { get; set; }
        public string shortDisplayName { get; set; }
        public List<Logo2> logos { get; set; }
        public List<Link2> links { get; set; }
    }

    public class CuratedRank
    {
        public int current { get; set; }
    }

    public class Competitor
    {
        public string id { get; set; }
        public string type { get; set; }
        public int order { get; set; }
        public string homeAway { get; set; }
        public Team2 team { get; set; }
        public CuratedRank curatedRank { get; set; }
    }

    public class Type2
    {
        public string id { get; set; }
        public string shortName { get; set; }
    }

    public class Market
    {
        public string id { get; set; }
        public string type { get; set; }
    }

    public class Media
    {
        public string shortName { get; set; }
    }

    public class Broadcast
    {
        public Type2 type { get; set; }
        public Market market { get; set; }
        public Media media { get; set; }
        public string lang { get; set; }
        public string region { get; set; }
    }

    public class Link3
    {
        public List<string> rel { get; set; }
        public string href { get; set; }
    }

    public class Ticket
    {
        public string id { get; set; }
        public string summary { get; set; }
        public string description { get; set; }
        public double maxPrice { get; set; }
        public double startingPrice { get; set; }
        public int numberAvailable { get; set; }
        public int totalPostings { get; set; }
        public List<Link3> links { get; set; }
    }

    public class Type3
    {
        public string id { get; set; }
        public string name { get; set; }
        public string state { get; set; }
        public bool completed { get; set; }
        public string description { get; set; }
        public string detail { get; set; }
        public string shortDetail { get; set; }
    }

    public class Status
    {
        public double clock { get; set; }
        public string displayClock { get; set; }
        public int period { get; set; }
        public Type3 type { get; set; }
    }

    public class Competition
    {
        public string id { get; set; }
        public string date { get; set; }
        public int attendance { get; set; }
        public Type type { get; set; }
        public bool neutralSite { get; set; }
        public bool boxscoreAvailable { get; set; }
        public bool ticketsAvailable { get; set; }
        public Venue venue { get; set; }
        public List<Competitor> competitors { get; set; }
        public List<object> notes { get; set; }
        public List<Broadcast> broadcasts { get; set; }
        public List<Ticket> tickets { get; set; }
        public Status status { get; set; }
    }

    public class Link4
    {
        public string language { get; set; }
        public List<string> rel { get; set; }
        public string href { get; set; }
        public string text { get; set; }
        public string shortText { get; set; }
        public bool isExternal { get; set; }
        public bool isPremium { get; set; }
    }

    public class NextEvent
    {
        public string id { get; set; }
        public string date { get; set; }
        public string name { get; set; }
        public string shortName { get; set; }
        public Season season { get; set; }
        public SeasonType seasonType { get; set; }
        public Week week { get; set; }
        public List<Competition> competitions { get; set; }
        public List<Link4> links { get; set; }
    }

    public class Team
    {
        public string id { get; set; }
        public string uid { get; set; }
        public string slug { get; set; }
        public string location { get; set; }
        public string name { get; set; }
        public string nickname { get; set; }
        public string abbreviation { get; set; }
        public string displayName { get; set; }
        public string shortDisplayName { get; set; }
        public string color { get; set; }
        public string alternateColor { get; set; }
        public bool isActive { get; set; }
        public List<Logo> logos { get; set; }
        public Record record { get; set; }
        public Groups groups { get; set; }
        public List<Link> links { get; set; }
        public List<NextEvent> nextEvent { get; set; }
        public string standingSummary { get; set; }
    }

    public class TeamInformation
    {
        public Team team { get; set; }
    }
}
