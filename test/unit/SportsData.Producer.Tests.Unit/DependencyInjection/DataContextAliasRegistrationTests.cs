using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Producer.DependencyInjection;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.DependencyInjection;

/// <summary>
/// The outbox writes captured messages into the DbContext instance registered
/// by AddDbContext. A handler that injects the abstract TeamSportDataContext
/// must therefore receive THAT instance, or its SaveChangesAsync persists a
/// different context and the message is silently discarded. These pin the
/// alias registration to one instance per scope, and pin the old registration
/// shape as the bug it was.
/// </summary>
public class DataContextAliasRegistrationTests
{
    private static ServiceProvider Build(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        services.AddDbContext<FootballDataContext>(o =>
            o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        register(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void Aliases_ResolveTheSameInstance_AsTheConcreteContext_WithinAScope()
    {
        using var provider = Build(s => s.AddTeamSportDataContextAliases<FootballDataContext>());
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<FootballDataContext>();
        var teamSport = scope.ServiceProvider.GetRequiredService<TeamSportDataContext>();
        var @base = scope.ServiceProvider.GetRequiredService<BaseDataContext>();

        teamSport.Should().BeSameAs(concrete, "the outbox writes to the AddDbContext instance; the alias must save the same one");
        @base.Should().BeSameAs(concrete);
    }

    [Fact]
    public void Aliases_AreScoped_NotShared_AcrossScopes()
    {
        using var provider = Build(s => s.AddTeamSportDataContextAliases<FootballDataContext>());

        TeamSportDataContext first, second;
        using (var scope = provider.CreateScope())
            first = scope.ServiceProvider.GetRequiredService<TeamSportDataContext>();
        using (var scope = provider.CreateScope())
            second = scope.ServiceProvider.GetRequiredService<TeamSportDataContext>();

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void TheOldRegistrationShape_ProducedASecondInstance_WhichIsTheBug()
    {
        // Documents WHY the alias exists: AddScoped<TAbstract, TConcrete> is a
        // separate registration with its own instance, not a forward. Any
        // outbox message captured on the AddDbContext instance and saved via
        // this one never reaches the broker.
        using var provider = Build(s =>
        {
            s.AddScoped<TeamSportDataContext, FootballDataContext>();
            s.AddScoped<BaseDataContext, FootballDataContext>();
        });
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<FootballDataContext>();
        var teamSport = scope.ServiceProvider.GetRequiredService<TeamSportDataContext>();

        teamSport.Should().NotBeSameAs(concrete);
    }
}
