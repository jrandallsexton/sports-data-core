using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// A named Player Pick'em scoring matrix. The MATRIX is data, the
    /// engine is code: rules are flat stat→points rows so operators can
    /// tune values without a deploy, and per-league selection later is a
    /// nullable FK on PickemGroup (absent → the IsDefault set).
    /// See docs/features/player-pickem/scoring.md.
    /// </summary>
    public class PlayerScoringRuleSet : CanonicalEntityBase<Guid>
    {
        /// <summary>Stable seed identity for the default "Standard" matrix (operator's chart, 2026-08-27).</summary>
        public static readonly Guid StandardRuleSetId = new("15c1e173-57ad-5c7e-99a1-c182d4c043ea");

        internal static readonly DateTime SeedStamp = new(2026, 8, 27, 0, 0, 0, DateTimeKind.Utc);
        public required string Name { get; set; }

        /// <summary>Exactly one set is the fallback for leagues without an explicit selection.</summary>
        public bool IsDefault { get; set; }

        public ICollection<PlayerScoringRule> Rules { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<PlayerScoringRuleSet>
        {
            public void Configure(EntityTypeBuilder<PlayerScoringRuleSet> builder)
            {
                builder.ToTable(nameof(PlayerScoringRuleSet));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name).HasMaxLength(100);

                builder.HasMany(x => x.Rules)
                    .WithOne(x => x.RuleSet)
                    .HasForeignKey(x => x.RuleSetId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Seeded default matrix — the operator's standard chart
                // (docs/features/player-pickem/scoring.md). Stable ids so
                // re-running migrations never duplicates; value tuning
                // happens with data updates, not schema changes.
                builder.HasData(new
                {
                    Id = PlayerScoringRuleSet.StandardRuleSetId,
                    Name = "Standard",
                    IsDefault = true,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                });
            }
        }
    }

    /// <summary>
    /// One scored stat: <c>points = value * Points / PerUnits</c>
    /// (fractional, rounded to 2dp by the engine). StatKey is the
    /// canonical <c>category.statName</c> from the Producer statline, or
    /// a <c>derived.*</c> key the engine computes (missed kicks, FG
    /// distance buckets) — derivations are structural and live in code;
    /// point VALUES live here.
    /// </summary>
    public class PlayerScoringRule : CanonicalEntityBase<Guid>
    {
        public Guid RuleSetId { get; set; }

        public PlayerScoringRuleSet RuleSet { get; set; } = null!;

        public required string StatKey { get; set; }

        public decimal Points { get; set; }

        public decimal PerUnits { get; set; } = 1m;

        public class EntityConfiguration : IEntityTypeConfiguration<PlayerScoringRule>
        {
            public void Configure(EntityTypeBuilder<PlayerScoringRule> builder)
            {
                builder.ToTable(nameof(PlayerScoringRule));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.StatKey).HasMaxLength(100);
                builder.Property(x => x.Points).HasPrecision(8, 2);
                builder.Property(x => x.PerUnits).HasPrecision(8, 2);
                builder.HasIndex(x => new { x.RuleSetId, x.StatKey }).IsUnique();

                builder.HasData(
                new
                {
                    Id = new Guid("807b8c25-7215-5f1c-98e8-464686858cd4"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "passing.passingYards",
                    Points = 1m,
                    PerUnits = 25m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("8f721f96-3c86-595f-896b-1e34ec3a0656"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "passing.passingTouchdowns",
                    Points = 6m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("e263379b-de5d-5fc8-8179-8914e67f8442"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "passing.interceptions",
                    Points = -2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("7e0e7e54-1fef-5e87-85fb-4485e1aca75c"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "passing.twoPtPass",
                    Points = 2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("56c9694e-079d-5b02-b955-5566f86e74f2"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "rushing.rushingYards",
                    Points = 1m,
                    PerUnits = 10m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("c7d3e6da-6cf1-5d77-bab5-a5d798c5cec2"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "rushing.rushingTouchdowns",
                    Points = 6m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("ea6842c9-d160-5acd-8587-100f54d4ec51"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "rushing.twoPtRush",
                    Points = 2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("ccc857a3-3b83-52f7-ab39-8c4c6739b3b8"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "receiving.receivingYards",
                    Points = 1m,
                    PerUnits = 10m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("559bcedf-28e3-5466-8be3-d8d70ee6c529"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "receiving.receivingTouchdowns",
                    Points = 6m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("d1c1bbec-7dd3-5a0f-bd7f-0a84597168f4"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "receiving.twoPtReception",
                    Points = 2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("b8a73ff8-3494-5459-838f-11e1e07f8720"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "fumbles.fumblesLost",
                    Points = -2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("6f215f20-0471-54d3-bee9-a26f1e66d1de"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "kicking.extraPointsMade",
                    Points = 1m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("5ad10eb5-739c-5a5a-afaa-7db306e03a6a"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.missedExtraPoints",
                    Points = -2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("c7d7861c-d887-586b-9788-d927009a1cc0"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMade17_39",
                    Points = 3m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("568ce9f4-6213-5254-81c1-d43e53f6e207"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMissed17_39",
                    Points = -2m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("666e3ffd-3d39-597a-9eb5-364003a9b997"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMade40_49",
                    Points = 4m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("33a100db-526d-5496-8c6e-f478226ab7c6"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMissed40_49",
                    Points = -1m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("629e7224-863f-5ec4-8f4c-4cbd89eb79f1"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMade50_59",
                    Points = 5m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                },
                new
                {
                    Id = new Guid("9ec911b7-d2d0-58cb-b99e-1b112752d462"),
                    RuleSetId = PlayerScoringRuleSet.StandardRuleSetId,
                    StatKey = "derived.fieldGoalsMade60Plus",
                    Points = 6m,
                    PerUnits = 1m,
                    CreatedUtc = PlayerScoringRuleSet.SeedStamp,
                    CreatedBy = Guid.Empty,
                });
            }
        }
    }
}
