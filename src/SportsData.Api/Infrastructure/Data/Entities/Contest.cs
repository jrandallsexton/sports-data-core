using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

using System.ComponentModel.DataAnnotations.Schema;

namespace SportsData.Api.Infrastructure.Data.Entities;

public class Contest : CanonicalEntityBase<Guid>
{
    public Guid ContestId { get; set; } // from Producer

    public Sport Sport { get; set; }

    public int SeasonYear { get; set; }

    public int SeasonWeek { get; set; }

    public DateTime StartUtc { get; set; }

    public Guid HomeFranchiseId { get; set; }

    public Guid AwayFranchiseId { get; set; }

    public double? Spread { get; set; }

    public double? OverUnder { get; set; }

    public bool IsVisible { get; set; } = true;

    public DateTime LastSyncedUtc { get; set; }

    // === Scoring Results ===
    public int? HomeScore { get; set; }

    public int? AwayScore { get; set; }

    public Guid? WinnerFranchiseId { get; set; }           // Straight-up

    public Guid? SpreadWinnerFranchiseId { get; set; }     // ATS winner

    public DateTime? FinalizedUtc { get; set; }

    // === Helpers (not mapped to DB) ===
    [NotMapped]
    public bool IsFinal => FinalizedUtc.HasValue;

    [NotMapped]
    public int? TotalScore =>
        HomeScore.HasValue && AwayScore.HasValue
            ? HomeScore + AwayScore
            : null;

    public class EntityConfiguration : IEntityTypeConfiguration<Contest>
    {
        public void Configure(EntityTypeBuilder<Contest> builder)
        {
            builder.ToTable("Contest");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.ContestId).IsUnique();
            builder.HasIndex(x => new { x.Sport, x.SeasonYear, x.StartUtc });
        }
    }
}