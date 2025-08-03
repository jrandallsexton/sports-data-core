using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitor : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public required Guid CompetitionId { get; set; }

    public required Guid FranchiseSeasonId { get; set; }

    public string? Type { get; set; }

    public int Order { get; set; }

    public string? HomeAway { get; set; }

    public bool Winner { get; set; }

    public int? CuratedRankCurrent { get; set; }

    public ICollection<CompetitionCompetitorLineScore> LineScores { get; set; } = [];

    public ICollection<CompetitionCompetitorScore> Scores { get; set; } = [];

    public ICollection<CompetitionCompetitorExternalId> ExternalIds { get; set; } = [];

    public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitor>
    {
        public void Configure(EntityTypeBuilder<CompetitionCompetitor> builder)
        {
            builder.ToTable(nameof(CompetitionCompetitor));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type)
                .HasMaxLength(20);

            builder.Property(x => x.HomeAway)
                .HasMaxLength(10);

            builder.Property(x => x.CuratedRankCurrent);

            builder.Property(x => x.Order)
                .IsRequired();

            builder.Property(x => x.Winner)
                .IsRequired();

            builder.Property(x => x.CompetitionId)
                .IsRequired();

            builder.Property(x => x.FranchiseSeasonId)
                .IsRequired();

            builder.HasOne<Competition>()
                .WithMany(x => x.Competitors)
                .HasForeignKey(x => x.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.LineScores)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Scores)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}