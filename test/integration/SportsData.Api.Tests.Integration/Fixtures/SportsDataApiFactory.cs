using MassTransit;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Tests.Integration.Fakes;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.Clients.Franchise;

namespace SportsData.Api.Tests.Integration.Fixtures;

/// <summary>
/// Hosts the real <c>SportsData.Api</c> in-process against a Testcontainers-managed
/// Postgres. Replaces prod-only subsystems (message bus, franchise HTTP client, auth)
/// with in-memory fakes so tests can exercise the controller → handler → DB path.
/// </summary>
public sealed class SportsDataApiFactory : WebApplicationFactory<Program>
{
    private readonly string _postgresConnectionString;

    public FakeFranchiseClientFactory FranchiseClientFactory { get; } = new();

    public SportsDataApiFactory(string postgresConnectionString)
    {
        _postgresConnectionString = postgresConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Program.TestingEnvironmentName);

        builder.ConfigureTestServices(services =>
        {
            // --- DbContext ----------------------------------------------------------
            // Program.cs skipped AddDataPersistence in Testing mode; register our own
            // pointing at the Testcontainers Postgres.
            services.RemoveAll<DbContextOptions<AppDataContext>>();
            services.AddDbContext<AppDataContext>(options =>
                options.UseNpgsql(_postgresConnectionString, npg =>
                {
                    npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(2), ["40001"]);
                }));

            // --- Messaging (no-op) --------------------------------------------------
            services.RemoveAll<IEventBus>();
            services.RemoveAll<IPublishEndpoint>();
            services.AddSingleton<IEventBus, NoOpBus>();
            services.AddSingleton<IPublishEndpoint, NoOpPublishEndpoint>();

            // --- Franchise client (fake) --------------------------------------------
            services.RemoveAll<IFranchiseClientFactory>();
            services.AddSingleton<IFranchiseClientFactory>(FranchiseClientFactory);

            // --- Auth ---------------------------------------------------------------
            // Replace the JWT/Firebase auth scheme with a deterministic fake. The
            // downstream FirebaseAuthenticationMiddleware will still run and load the
            // seeded test user via IUserService.
            services.AddAuthentication(opts =>
            {
                opts.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                opts.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                opts.DefaultScheme = TestAuthHandler.SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });
        });
    }
}
