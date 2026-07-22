using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

/// <summary>
/// A single ESPN <c>situationNotes[]</c> entry (type + text) for a baseball
/// situation snapshot — captured verbatim so nothing ESPN publishes is dropped.
/// </summary>
public class BaseballCompetitionSituationNote : CanonicalEntityBase<Guid>
{
    public Guid SituationId { get; set; }

    public BaseballCompetitionSituation Situation { get; set; } = null!;

    public string? Type { get; set; }

    public string? Text { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionSituationNote>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionSituationNote> builder)
        {
            builder.ToTable(nameof(BaseballCompetitionSituationNote));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Type).HasMaxLength(64);
            builder.Property(x => x.Text).HasMaxLength(512);

            builder.HasIndex(x => x.SituationId);
        }
    }
}
