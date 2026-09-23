using Microsoft.Extensions.DependencyInjection;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.DependencyInjection;

public static class DataContextAliasRegistration
{
    /// <summary>
    /// Exposes the sport's concrete DbContext under its abstract base types
    /// as the SAME scoped instance.
    /// </summary>
    /// <remarks>
    /// This replaced <c>AddScoped&lt;TeamSportDataContext, FootballDataContext&gt;()</c>,
    /// which creates a SECOND FootballDataContext per scope. MassTransit's EF
    /// outbox writes captured messages into the instance registered by
    /// AddDbContext; a handler that injects TeamSportDataContext and calls
    /// SaveChangesAsync saves the other one, so its outbox rows are never
    /// persisted and the message is discarded when the scope ends, with no
    /// error. Found 2026-09-23: EnrichFranchiseSeasonHandler had never
    /// delivered FranchiseSeasonEnrichmentCompleted; the exchange did not
    /// even exist on the Producer brokers. Consumer-scoped handlers were
    /// unaffected because MassTransit's consumer outbox filter saves the
    /// outbox context itself; Hangfire- and HTTP-scoped handlers were not.
    /// See DataContextAliasRegistrationTests.
    /// </remarks>
    public static IServiceCollection AddTeamSportDataContextAliases<TContext>(this IServiceCollection services)
        where TContext : TeamSportDataContext
    {
        services.AddScoped<TeamSportDataContext>(sp => sp.GetRequiredService<TContext>());
        services.AddScoped<BaseDataContext>(sp => sp.GetRequiredService<TContext>());
        return services;
    }
}
