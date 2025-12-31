namespace SportsData.Api.Application.UI.Conferences.Dtos;

public class ConferenceNameAndSlugDto
{
    public required string Division { get; set; }

    public required string ShortName { get; set; }

    public required string Slug { get; set; }
}
