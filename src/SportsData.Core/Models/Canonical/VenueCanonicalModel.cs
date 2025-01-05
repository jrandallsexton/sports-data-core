namespace SportsData.Core.Models.Canonical
{
    public class VenueCanonicalModel : CanonicalModelBase
    {

        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

    }
}
