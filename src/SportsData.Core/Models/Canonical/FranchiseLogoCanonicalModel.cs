using System;

namespace SportsData.Core.Models.Canonical
{
    public class FranchiseeLogoCanonicalModel(
        Guid franchiseId,
        string url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid FranchiseId { get; init; } = franchiseId;
    }
}
