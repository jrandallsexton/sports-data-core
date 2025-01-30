using System;

namespace SportsData.Core.Models.Canonical
{
    public class AthleteCanonicalModel : CanonicalModelBase
    {
        public string LastName { get; set; }

        public string FirstName { get; set; }

        public string DisplayName { get; set; }

        public string ShortName { get; set; }

        public decimal WeightLb { get; set; }

        public string WeightDisplay { get; set; }

        public decimal HeightIn { get; set; }

        public string HeightDisplay { get; set; }

        public int Age { get; set; }

        public DateTime DoB { get; set; }

        public int CurrentExperience { get; set; }

        public bool IsActive { get; set; }

        public Guid PositionId { get; set; }

        public string PositionName { get; set; }
    }
}
