using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class FranchiseSeasonLogoDto(
        Guid franchiseSeasonId,
        string url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid FranchiseSeasonId { get; init; } = franchiseSeasonId;
    }
}
