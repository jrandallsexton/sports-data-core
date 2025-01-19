using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeason : EntityBase<Guid>
    {
        public Guid FranchiseId { get; set; }

        public int Season { get; set; }

        public string Slug { get; set; }

        public string Location { get; set; }

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public string DisplayName { get; set; }

        public string DisplayNameShort { get; set; }

        public string ColorCodeHex { get; set; }

        public string? ColorCodeAltHex { get; set; }

        public bool IsActive { get; set; }

        public bool IsAllStar { get; set; }

        public List<FranchiseSeasonLogo> Logos { get; set; } = [];

        public int Wins { get; set; }

        public int Losses { get; set; }

        public int Ties { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeason>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeason> builder)
            {
                builder.ToTable("FranchiseSeason");
                builder.HasKey(t => t.Id);
                builder.HasOne<Franchise>()
                    .WithMany(x => x.Seasons)
                    .HasForeignKey(x => x.FranchiseId);
            }
        }
    }
}
