﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeason : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid FranchiseId { get; set; }

        public Franchise Franchise { get; set; } = default!;

        public Guid? VenueId { get; set; }

        public Guid? GroupSeasonId { get; set; }

        public GroupSeason? GroupSeason { get; set; }

        public int SeasonYear { get; set; }

        public required string Slug { get; set; }

        public required string Location { get; set; }

        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string DisplayName { get; set; }

        public required string DisplayNameShort { get; set; }

        public required string ColorCodeHex { get; set; }

        public string? ColorCodeAltHex { get; set; }

        public bool IsActive { get; set; }

        public bool IsAllStar { get; set; }

        public ICollection<FranchiseSeasonLogo> Logos { get; set; } = new List<FranchiseSeasonLogo>();

        public int Wins { get; set; }

        public int Losses { get; set; }

        public int Ties { get; set; }

        public ICollection<FranchiseSeasonExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public ICollection<FranchiseSeasonRanking> Rankings { get; set; } = [];

        public ICollection<FranchiseSeasonRecord> Records { get; set; } = [];

        public ICollection<FranchiseSeasonRecordAts> RecordsAts { get; set; } = [];

        public ICollection<FranchiseSeasonStatisticCategory> Statistics { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeason>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeason> builder)
            {
                builder.ToTable(nameof(FranchiseSeason));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Slug).HasMaxLength(100);
                builder.Property(t => t.Location).HasMaxLength(100);
                builder.Property(t => t.Name).HasMaxLength(100);
                builder.Property(t => t.Abbreviation).HasMaxLength(20);
                builder.Property(t => t.DisplayName).HasMaxLength(100);
                builder.Property(t => t.DisplayNameShort).HasMaxLength(50);
                builder.Property(t => t.ColorCodeHex).HasMaxLength(7);
                builder.Property(t => t.ColorCodeAltHex).HasMaxLength(7);

                builder.HasOne(x => x.Franchise)
                    .WithMany(f => f.Seasons)
                    .HasForeignKey(x => x.FranchiseId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne<Venue>()
                    .WithMany()
                    .HasForeignKey(x => x.VenueId)
                    .OnDelete(DeleteBehavior.SetNull);

                builder.HasOne(x => x.GroupSeason)
                    .WithMany(x => x.FranchiseSeasons)
                    .HasForeignKey(x => x.GroupSeasonId)
                    .OnDelete(DeleteBehavior.SetNull);

                builder.HasMany(x => x.Records)
                    .WithOne(r => r.Season)
                    .HasForeignKey(r => r.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Rankings)
                    .WithOne(r => r.FranchiseSeason)
                    .HasForeignKey(r => r.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
