namespace SportsData.Core.Dtos.Canonical
{
    public record PositionDto : DtoBase
    {
        public string Name { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string Abbreviation { get; set; } = null!;
    }
}
