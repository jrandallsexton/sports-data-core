using System;

namespace SportsData.Core.Models.Canonical
{
    public class FranchiseSeasonLogoCanonicalModel(
        Guid franchiseSeasonId,
        string url,
        int? height,
        int? width) : CanonicalLogoModelBase(url, height, width)
    {
        public Guid FranchiseSeasonId { get; init; } = franchiseSeasonId;
    }
}
