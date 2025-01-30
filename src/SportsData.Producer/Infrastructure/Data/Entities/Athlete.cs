using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Athlete : CanonicalEntityBase<Guid>
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

        // TODO: Birth location info

        public Guid? FranchiseId { get; set; }

        public Guid? FranchiseSeasonId { get; set; }

        public Guid CurrentPosition { get; set; }

        public Position Position { get; set; }

        public int CurrentExperience { get; set; }

        public bool IsActive { get; set; }

        public List<AthleteSeason> Seasons { get; set; }

        public List<AthleteImage> Images { get; set; }

        public List<AthleteExternalId> ExternalIds { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Athlete>
        {
            public void Configure(EntityTypeBuilder<Athlete> builder)
            {
                builder.ToTable("Athlete");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
