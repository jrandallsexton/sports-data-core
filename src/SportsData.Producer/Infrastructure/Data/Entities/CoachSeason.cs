using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CoachSeason : CanonicalEntityBase<Guid>
{
    public Guid CoachId { get; set; }

    public Coach? Coach { get; set; }

    public Guid FranchiseSeasonId { get; set; }

    public string? Title { get; set; }

    public FranchiseSeason? FranchiseSeason { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<CoachSeason>
    {
        public void Configure(EntityTypeBuilder<CoachSeason> builder)
        {
            builder.ToTable("CoachSeason");
            builder.HasKey(t => t.Id);
            builder.HasOne(cs => cs.Coach)
                .WithMany(c => c.Seasons)
                .HasForeignKey(cs => cs.CoachId);
            builder.HasOne(cs => cs.FranchiseSeason)
                .WithMany()
                .HasForeignKey(cs => cs.FranchiseSeasonId);
            builder.Property(t => t.Title).HasMaxLength(100);
        }
    }
}