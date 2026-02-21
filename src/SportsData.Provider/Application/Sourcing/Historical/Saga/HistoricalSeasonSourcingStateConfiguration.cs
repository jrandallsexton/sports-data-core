using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Provider.Application.Sourcing.Historical.Saga;

/// <summary>
/// Entity Framework configuration for HistoricalSeasonSourcingState saga persistence.
/// </summary>
public class HistoricalSeasonSourcingStateConfiguration : IEntityTypeConfiguration<HistoricalSeasonSourcingState>
{
    public void Configure(EntityTypeBuilder<HistoricalSeasonSourcingState> builder)
    {
        builder.ToTable("HistoricalSourcingSagas");
        
        // CorrelationId is the primary key for MassTransit sagas
        builder.HasKey(x => x.CorrelationId);
        builder.Property(x => x.CorrelationId).ValueGeneratedNever();
        
        // CurrentState is required for state machine
        builder.Property(x => x.CurrentState)
            .HasMaxLength(64)
            .IsRequired();
        
        // Composite index for correlation by Sport + SeasonYear
        builder.HasIndex(x => new { x.Sport, x.SeasonYear })
            .HasDatabaseName("IX_HistoricalSourcingSagas_Sport_Season");
        
        // Index on CurrentState for querying active sagas
        builder.HasIndex(x => x.CurrentState)
            .HasDatabaseName("IX_HistoricalSourcingSagas_CurrentState");
        
        // Index on StartedUtc for observability (finding stalled sagas)
        builder.HasIndex(x => x.StartedUtc)
            .HasDatabaseName("IX_HistoricalSourcingSagas_StartedUtc");
    }
}
