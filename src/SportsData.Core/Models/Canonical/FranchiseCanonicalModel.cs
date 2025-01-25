using SportsData.Core.Common;

namespace SportsData.Core.Models.Canonical
{
    public class FranchiseCanonicalModel : CanonicalModelBase
    {
        public Sport Sport { get; set; }

        public string Name { get; set; }

        public string Nickname { get; set; }

        public string Abbreviation { get; set; }

        public string DisplayName { get; set; }

        public string DisplayNameShort { get; set; }

        public string ColorCodeHex { get; set; }

        public string? ColorCodeAltHex { get; set; }

        public string Slug { get; set; }
    }
}
