using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public abstract class TeamAthleteBase : AthleteBase
    {
        public Guid? FranchiseId { get; set; }

        public Guid? FranchiseSeasonId { get; set; }

        public Guid? PositionId { get; set; }

        public AthletePosition? Position { get; set; }

        public class TeamAthleteConfiguration : IEntityTypeConfiguration<TeamAthleteBase>
        {
            public void Configure(EntityTypeBuilder<TeamAthleteBase> builder)
            {
                builder.Property(a => a.FranchiseId);

                builder.Property(a => a.FranchiseSeasonId);

                builder.HasOne(a => a.Position)
                    .WithMany()
                    .HasForeignKey(a => a.PositionId);
            }
        }
    }
}
