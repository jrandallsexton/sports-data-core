using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionCompetitor : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public required Guid CompetitionId { get; set; }

    public Competition Competition { get; set; } = null!;

    public required Guid FranchiseSeasonId { get; set; }

    public string? Type { get; set; }

    public int Order { get; set; }

    public required string HomeAway { get; set; }

    /// <summary>
    /// This is an enriched field; not in original data
    /// </summary>
    public int? Points { get; set; }

    public bool Winner { get; set; }

    public int? CuratedRankCurrent { get; set; }

    public ICollection<CompetitionCompetitorStatistic> Statistics { get; set; } = [];

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
                .IsRequired()
                .HasMaxLength(10);

            builder.Property(x => x.CuratedRankCurrent);
            builder.Property(x => x.Order).IsRequired();
            builder.Property(x => x.Winner).IsRequired();
            builder.Property(x => x.Points);
            builder.Property(x => x.CompetitionId).IsRequired();
            builder.Property(x => x.FranchiseSeasonId).IsRequired();

            // FK: Competition (parent) -> Competitors (children)
            builder.HasOne(cc => cc.Competition)
                .WithMany(c => c.Competitors)
                .HasForeignKey(cc => cc.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);

            // FK: FranchiseSeason (reference) -> Competitors
            // Prefer Restrict so deleting a FranchiseSeason doesn't cascade a wave into contests
            builder.HasOne<FranchiseSeason>()
                .WithMany()
                .HasForeignKey(x => x.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            // Children: LineScores
            builder.HasMany(x => x.LineScores)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Children: Scores
            builder.HasMany(x => x.Scores)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // NEW: Children: Statistics
            builder.HasMany(x => x.Statistics)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // ExternalIds (assuming FK property CompetitionCompetitorId on the child)
            builder.HasMany(x => x.ExternalIds)
                .WithOne(x => x.CompetitionCompetitor)
                .HasForeignKey(x => x.CompetitionCompetitorId)
                .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(x => x.CompetitionId);
            builder.HasIndex(x => x.FranchiseSeasonId);

            // Uniqueness within a competition:
            // - exactly one 'home' and one 'away'
            builder.HasIndex(x => new { x.CompetitionId, x.HomeAway })
                   .IsUnique();

            // - stable ordering (0/1 or 1/2) per competition
            builder.HasIndex(x => new { x.CompetitionId, x.Order })
                   .IsUnique();
        }
    }

}