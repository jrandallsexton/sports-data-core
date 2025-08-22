namespace SportsData.Api.Infrastructure.Data.Canonical.Models
{
    public class ConferenceDivisionNameAndSlugDto
    {
        public required string Division { get; set; }

        public required string ShortName { get; set; }

        public required string Slug { get; set; }
    }
}
