namespace SportsData.Core.Dtos.Canonical
{
    public record AddressDto
    {
        public required string City { get; init; }

        public required string State { get; init; }
    }
}
