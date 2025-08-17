using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseSeasonRanking : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public Guid FranchiseSeasonId { get; set; }

    public FranchiseSeason FranchiseSeason { get; set; } = null!;

    public Guid FranchiseId { get; set; }

    public Franchise Franchise { get; set; } = null!;

    public int SeasonYear { get; set; }
    
    public string Name { get; set; } = string.Empty;

    public string ShortName { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public DateTime? Date { get; set; }

    public string Headline { get; set; } = string.Empty;

    public string ShortHeadline { get; set; } = string.Empty;

    public bool DefaultRanking { get; set; }

    public DateTime? LastUpdated { get; set; }

    public FranchiseSeasonRankingOccurrence Occurrence { get; set; } = null!;

    public ICollection<FranchiseSeasonRankingNote> Notes { get; set; } = [];

    public FranchiseSeasonRankingDetail Rank { get; set; } = null!;

    public ICollection<FranchiseSeasonRankingExternalId> ExternalIds { get; set; } = [];

    public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRanking>
    {
        public void Configure(EntityTypeBuilder<FranchiseSeasonRanking> builder)
        {
            builder.ToTable(nameof(FranchiseSeasonRanking));

            builder.HasKey(e => e.Id);

            builder.Property(e => e.SeasonYear)
                .IsRequired();

            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.ShortName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(e => e.Headline)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(e => e.ShortHeadline)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(e => e.Date)
                .HasMaxLength(40);

            builder.Property(e => e.LastUpdated)
                .HasMaxLength(40);

            builder.Property(e => e.DefaultRanking)
                .IsRequired();

            // Relationships
            builder.HasOne(e => e.Franchise)
                .WithMany()
                .HasForeignKey(e => e.FranchiseId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(e => e.FranchiseSeason)
                .WithMany(fs => fs.Rankings)
                .HasForeignKey(e => e.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Rank)
                .WithOne(r => r.FranchiseSeasonRanking)
                .HasForeignKey<FranchiseSeasonRankingDetail>(r => r.FranchiseSeasonRankingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.Occurrence)
                .WithOne(o => o.Ranking)
                .HasForeignKey<FranchiseSeasonRankingOccurrence>(o => o.RankingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.Notes)
                .WithOne(n => n.Ranking)
                .HasForeignKey(n => n.RankingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(e => e.ExternalIds)
                .WithOne(x => x.Ranking)
                .HasForeignKey(x => x.RankingId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}