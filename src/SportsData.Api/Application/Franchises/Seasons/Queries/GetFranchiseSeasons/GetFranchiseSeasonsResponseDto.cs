using SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasonById;

namespace SportsData.Api.Application.Franchises.Seasons.Queries.GetFranchiseSeasons;

public class GetFranchiseSeasonsResponseDto
{
    public Guid FranchiseId { get; set; }

    public string FranchiseSlug { get; set; } = null!;

    public List<FranchiseSeasonResponseDto> Items { get; set; } = new();

    // HATEOAS
    public Dictionary<string, Uri> Links { get; set; } = new();
}
