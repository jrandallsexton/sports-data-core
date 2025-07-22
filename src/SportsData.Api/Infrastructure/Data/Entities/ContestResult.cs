using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities;

public class ContestResult : CanonicalEntityBase<Guid>
{
    public Guid ContestId { get; set; } // From API.Contest

    public Sport Sport { get; set; }

    public int SeasonYear { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime? EndUtc { get; set; }

    public Guid HomeFranchiseId { get; set; }

    public Guid AwayFranchiseId { get; set; }

    public Guid? WinningFranchiseId { get; set; }

    public int HomeScore { get; set; }

    public int AwayScore { get; set; }

    public double? OverUnder { get; set; }

    public double? Spread { get; set; } // Positive favors home

    public bool WasCanceled { get; set; }

    public bool WentToOvertime { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<ContestResult>
    {
        public void Configure(EntityTypeBuilder<ContestResult> builder)
        {
            builder.ToTable(nameof(ContestResult));
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.ContestId).IsUnique();
            builder.HasIndex(x => new { x.Sport, x.SeasonYear });
        }
    }
}