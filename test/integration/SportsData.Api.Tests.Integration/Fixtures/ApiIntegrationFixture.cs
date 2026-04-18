using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Tests.Integration.Fakes;

using Testcontainers.PostgreSql;

using Xunit;

namespace SportsData.Api.Tests.Integration.Fixtures;

/// <summary>
/// Collection-level fixture. Spins up one Postgres container per test run, boots
/// the API via <see cref="SportsDataApiFactory"/>, applies migrations, and seeds
/// the canonical test user. Individual tests call <see cref="ResetDatabaseAsync"/>
/// to scrub mutable tables before exercising a scenario.
/// </summary>
public sealed class ApiIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public SportsDataApiFactory Factory { get; private set; } = null!;

    public Guid TestUserId { get; private set; }

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("sd_api_test")
            .WithUsername("sd")
            .WithPassword("sd")
            .Build();

        await _postgres.StartAsync();

        Factory = new SportsDataApiFactory(_postgres.GetConnectionString());

        // Applying migrations also builds the DI container — forces lazy issues to surface early.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
        await db.Database.MigrateAsync();

        TestUserId = await SeedTestUserAsync(db);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>
    /// Wipes mutable test-scoped tables between scenarios. The seeded test user stays.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();

        await db.Database.ExecuteSqlRawAsync(
            """TRUNCATE TABLE "PickemGroup" RESTART IDENTITY CASCADE;""");
    }

    /// <summary>Gives tests a scoped <see cref="AppDataContext"/> for direct assertions.</summary>
    public IServiceScope CreateScope() => Factory.Services.CreateScope();

    private static async Task<Guid> SeedTestUserAsync(AppDataContext db)
    {
        var existing = await db.Users
            .FirstOrDefaultAsync(u => u.FirebaseUid == TestIdentity.FirebaseUid);

        if (existing is not null) return existing.Id;

        var user = new User
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.Empty,
            FirebaseUid = TestIdentity.FirebaseUid,
            Email = TestIdentity.Email,
            EmailVerified = true,
            SignInProvider = "test",
            DisplayName = TestIdentity.DisplayName,
            IsAdmin = true,
            LastLoginUtc = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }
}

[CollectionDefinition(nameof(ApiIntegrationCollection))]
public sealed class ApiIntegrationCollection : ICollectionFixture<ApiIntegrationFixture>
{
    // Marker class — xUnit requires this shape for collection fixtures.
}
