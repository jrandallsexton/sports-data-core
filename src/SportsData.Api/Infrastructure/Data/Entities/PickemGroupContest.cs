using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupContest : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid ContestId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupContest>
        {
            public void Configure(EntityTypeBuilder<PickemGroupContest> builder)
            {
                builder.ToTable(nameof(PickemGroupContest));
                builder.HasKey(gc => gc.Id);

                builder.HasIndex(gc => new {
                    gc.PickemGroupId,
                    gc.ContestId
                }).IsUnique();

                builder.HasIndex(gc => new {
                    gc.PickemGroupId,
                    gc.SeasonYear,
                    gc.SeasonWeek
                });
            }
        }
    }
}