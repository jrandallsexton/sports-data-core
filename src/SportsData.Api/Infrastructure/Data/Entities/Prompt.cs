using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// A prompt as a first-class entity: Guid identity, metadata, and the
    /// instruction TEXT itself. The database is private (the repo is
    /// public), so text lives here — no blob round-trip, no Azurite for
    /// local dev. The legacy blob container remains only as a one-time
    /// import source (and for game recaps until they unify onto this).
    ///
    /// Default resolution slot = (Type, Sport, WithStats): the provider
    /// picks the IsDefault row for the slot, preferring sport-specific over
    /// Sport=null (any sport). Handler logic keeps at most one default per
    /// slot. Captures store the exact text sent regardless, so editing a
    /// prompt row never rewrites history.
    /// </summary>
    public class Prompt : CanonicalEntityBase<Guid>
    {
        /// <summary>Human-readable version name (e.g. "prediction-insights-v1") — becomes PromptVersion on captures.</summary>
        public required string Name { get; set; }

        public PromptType Type { get; set; } = PromptType.MatchupPreview;

        /// <summary>Null = applies to any sport; a sport-specific default outranks it.</summary>
        public Sport? Sport { get; set; }

        /// <summary>Which stats-variant slot this prompt serves (mirrors the hasStats selection).</summary>
        public bool WithStats { get; set; }

        /// <summary>The active prompt for its (Type, Sport, WithStats) slot.</summary>
        public bool IsDefault { get; set; }

        /// <summary>Optional operator note for the management UI.</summary>
        public string? Description { get; set; }

        /// <summary>The full instruction text.</summary>
        public required string Text { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Prompt>
        {
            public void Configure(EntityTypeBuilder<Prompt> builder)
            {
                builder.ToTable(nameof(Prompt));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
                builder.HasIndex(x => x.Name).IsUnique();

                builder.Property(x => x.Type)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.Sport)
                    .HasConversion<int?>();

                builder.Property(x => x.Description).HasMaxLength(256);

                builder.Property(x => x.Text).IsRequired();

                // Default lookups: slot scan is tiny, but index the flag for
                // the management UI's "what's live" view.
                builder.HasIndex(x => new { x.Type, x.IsDefault });
            }
        }
    }

    public enum PromptType
    {
        MatchupPreview = 0,

        /// <summary>Not yet served from the DB — game recaps still load from blob; unify later.</summary>
        GameRecap = 1
    }
}
