using SportsData.Core.Common;

namespace SportsData.Core.Dtos.Canonical
{
    public record FranchiseDto : DtoBase
    {
        public Sport Sport { get; init; }

        public string Name { get; init; } = null!;

        public string Nickname { get; init; } = null!;

        public string Abbreviation { get; init; } = null!;

        public string DisplayName { get; init; } = null!;

        public string? DisplayNameShort { get; init; }

        public string ColorCodeHex { get; init; } = null!;

        public string? ColorCodeAltHex { get; init; }

        public string Slug { get; init; } = null!;
    }
}