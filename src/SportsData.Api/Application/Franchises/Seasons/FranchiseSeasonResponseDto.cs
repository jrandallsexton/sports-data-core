namespace SportsData.Api.Application.Franchises.Seasons;

public class FranchiseSeasonResponseDto
{
    public Guid Id { get; set; }

    public Guid FranchiseId { get; set; }

    public int SeasonYear { get; set; }

    public string Slug { get; set; } = null!;

    public string Location { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Abbreviation { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string DisplayNameShort { get; set; } = null!;

    public string ColorCodeHex { get; set; } = null!;

    public string? ColorCodeAltHex { get; set; }

    public bool IsActive { get; set; }

    public int Wins { get; set; }

    public int Losses { get; set; }

    public int Ties { get; set; }

    public int ConferenceWins { get; set; }

    public int ConferenceLosses { get; set; }

    public int ConferenceTies { get; set; }

    // HATEOAS
    public Uri Ref { get; set; } = null!;

    public Dictionary<string, Uri> Links { get; set; } = new();
}
