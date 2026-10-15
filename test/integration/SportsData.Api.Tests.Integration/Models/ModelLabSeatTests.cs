using System.Data.Common;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using SportsData.Api.Application.Models.Commands.CreateModel;
using SportsData.Api.Application.Models.Commands.UpdateModel;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Tests.Integration.Fixtures;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Integration.Models;

/// <summary>
/// The lab-seat swap against real PostgreSQL. IX_Model_SingleActiveLabSeat
/// is a partial unique index the InMemory unit tests cannot enforce, and EF
/// orders same-table updates by key — not by an index it cannot see depends
/// on IsActive — so these pin the key order both ways and assert the swap
/// still lands (docs/features/model-family.md).
/// </summary>
[Collection(nameof(ApiIntegrationCollection))]
public sealed class ModelLabSeatTests
{
    private readonly ApiIntegrationFixture _fixture;
    private readonly Dictionary<Guid, Guid> _providerByFamily = [];

    public ModelLabSeatTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateModel_ActivatingAVersion_SwapsTheSeat_InEitherKeyOrder(bool incomingSortsFirst)
    {
        // Same random prefix, last byte decides: Guid.CompareTo walks the
        // fields in order, so only the final byte differs.
        var prefix = Guid.NewGuid().ToString()[..34];
        var low = Guid.Parse(prefix + "01");
        var high = Guid.Parse(prefix + "02");
        var incomingId = incomingSortsFirst ? low : high;
        var incumbentId = incomingSortsFirst ? high : low;

        var familyId = await SeedFamilyAsync();
        await SeedModelAsync(incumbentId, familyId, "incumbent", isActive: true);
        await SeedModelAsync(incomingId, familyId, "successor", isActive: false);

        using (var scope = _fixture.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IUpdateModelCommandHandler>()
                .ExecuteAsync(new UpdateModelCommand
                {
                    ModelId = incomingId,
                    ModelFamilyId = familyId,
                    IsActive = true
                }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        var active = await ActiveSeatIdsAsync(familyId);
        active.Should().Equal(incomingId);
    }

    [Fact]
    public async Task CreateModel_OpenRouterVersion_SwapsTheSeat()
    {
        var familyId = await SeedFamilyAsync();
        await SeedModelAsync(Guid.NewGuid(), familyId, "incumbent", isActive: true);

        Guid createdId;
        using (var scope = _fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
            var providerId = await db.Models.Where(m => m.ModelFamilyId == familyId)
                .Select(m => m.ModelProviderId).FirstAsync();

            var result = await scope.ServiceProvider.GetRequiredService<ICreateModelCommandHandler>()
                .ExecuteAsync(new CreateModelCommand
                {
                    ModelProviderId = providerId,
                    ModelFamilyId = familyId,
                    Name = $"successor-{familyId:N}",
                    ApiModelId = $"test/successor-{familyId:N}",
                    Gateway = ModelGateway.OpenRouter
                }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            createdId = result.Value;
        }

        var active = await ActiveSeatIdsAsync(familyId);
        active.Should().Equal(createdId);
    }

    [Fact]
    public async Task UpdateModel_RetryAfterFailedCommit_StillSwapsTheSeat()
    {
        // The first commit fails with a transient serialization error, so
        // the execution strategy rolls back and re-runs the delegate. The
        // retry must re-issue the vacate and the activation, not trust
        // entities tracked from the rolled-back attempt.
        var familyId = await SeedFamilyAsync();
        var incumbentId = Guid.NewGuid();
        var incomingId = Guid.NewGuid();
        await SeedModelAsync(incumbentId, familyId, "incumbent", isActive: true);
        await SeedModelAsync(incomingId, familyId, "successor", isActive: false);

        var failOnce = new FailFirstCommitInterceptor();
        await using (var db = CreateRetryingContext(failOnce))
        {
            var result = await new UpdateModelCommandHandler(db, new DateTimeProvider(), NullLogger<UpdateModelCommandHandler>.Instance)
                .ExecuteAsync(new UpdateModelCommand
                {
                    ModelId = incomingId,
                    ModelFamilyId = familyId,
                    IsActive = true
                }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        failOnce.Failures.Should().Be(1);
        var active = await ActiveSeatIdsAsync(familyId);
        active.Should().Equal(incomingId);
    }

    [Fact]
    public async Task CreateModel_RetryAfterFailedCommit_StillSwapsTheSeat()
    {
        var familyId = await SeedFamilyAsync();
        await SeedModelAsync(Guid.NewGuid(), familyId, "incumbent", isActive: true);

        var failOnce = new FailFirstCommitInterceptor();
        Guid createdId;
        await using (var db = CreateRetryingContext(failOnce))
        {
            var result = await new CreateModelCommandHandler(db, new DateTimeProvider(), NullLogger<CreateModelCommandHandler>.Instance)
                .ExecuteAsync(new CreateModelCommand
                {
                    ModelProviderId = _providerByFamily[familyId],
                    ModelFamilyId = familyId,
                    Name = $"successor-{familyId:N}",
                    ApiModelId = $"test/successor-{familyId:N}",
                    Gateway = ModelGateway.OpenRouter
                }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            createdId = result.Value;
        }

        failOnce.Failures.Should().Be(1);
        var active = await ActiveSeatIdsAsync(familyId);
        active.Should().Equal(createdId);
    }

    [Fact]
    public async Task UpdateModel_SeatSwappedAwayBeforeTheAttempt_TakesTheSeatBack()
    {
        // The handler reads the model as the active seat holder (so a plain
        // metadata edit would not take a seat). Before its transaction does
        // anything, a concurrent swap deactivates it and seats another
        // version. The update keeps IsActive = true, so it must decide from
        // the reloaded row that it now takes the seat and vacate the new
        // holder, not collide on the index.
        var familyId = await SeedFamilyAsync();
        var editedId = Guid.NewGuid();
        var rivalId = Guid.NewGuid();
        await SeedModelAsync(editedId, familyId, "edited", isActive: true);
        await SeedModelAsync(rivalId, familyId, "rival", isActive: false);

        var connectionString = ConnectionString();
        var swap = new OnFirstTransactionStartInterceptor(async () =>
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE \"Model\" SET \"IsActive\" = FALSE WHERE \"Id\" = @edited; " +
                "UPDATE \"Model\" SET \"IsActive\" = TRUE WHERE \"Id\" = @rival;", conn);
            cmd.Parameters.AddWithValue("edited", editedId);
            cmd.Parameters.AddWithValue("rival", rivalId);
            await cmd.ExecuteNonQueryAsync();
        });

        await using (var db = CreateRetryingContext(swap))
        {
            var result = await new UpdateModelCommandHandler(db, new DateTimeProvider(), NullLogger<UpdateModelCommandHandler>.Instance)
                .ExecuteAsync(new UpdateModelCommand
                {
                    ModelId = editedId,
                    ModelFamilyId = familyId,
                    CutoffEvidence = "metadata edit",
                    IsActive = true
                }, CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
        }

        swap.Fired.Should().BeTrue();
        var active = await ActiveSeatIdsAsync(familyId);
        active.Should().Equal(editedId);
    }

    private string ConnectionString()
    {
        using var scope = _fixture.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AppDataContext>()
            .Database.GetConnectionString()!;
    }

    /// <summary>Runs a side effect once, as the handler's first transaction opens.</summary>
    private sealed class OnFirstTransactionStartInterceptor(Func<Task> sideEffect) : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            if (!Fired)
            {
                Fired = true;
                await sideEffect();
            }

            return result;
        }
    }

    /// <summary>Same retry policy as production (Core ServiceRegistration), shorter delay.</summary>
    private AppDataContext CreateRetryingContext(IInterceptor interceptor)
    {
        var connectionString = ConnectionString();

        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseNpgsql(connectionString, b => b.EnableRetryOnFailure(5, TimeSpan.FromMilliseconds(200), ["40001"]))
            .AddInterceptors(interceptor)
            .Options;

        return new AppDataContext(options);
    }

    /// <summary>Fails the first commit with a transient serialization error (40001).</summary>
    private sealed class FailFirstCommitInterceptor : DbTransactionInterceptor
    {
        public int Failures { get; private set; }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Failures == 0)
            {
                Failures++;
                throw new PostgresException("injected serialization failure", "ERROR", "ERROR", "40001");
            }

            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }

    private async Task<Guid> SeedFamilyAsync()
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();

        var family = new ModelFamily { Id = Guid.NewGuid(), Name = $"family-{Guid.NewGuid():N}" };
        var provider = new ModelProvider
        {
            Id = Guid.NewGuid(),
            Name = $"provider-{family.Id:N}",
            Kind = ModelProviderKind.GatewayOnly
        };
        db.ModelFamilies.Add(family);
        db.ModelProviders.Add(provider);
        await db.SaveChangesAsync();

        _providerByFamily[family.Id] = provider.Id;
        return family.Id;
    }

    private async Task SeedModelAsync(Guid id, Guid familyId, string label, bool isActive)
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();

        db.Models.Add(new Model
        {
            Id = id,
            ModelProviderId = _providerByFamily[familyId],
            ModelFamilyId = familyId,
            Name = $"{label}-{id:N}",
            ApiModelId = $"test/{label}-{id:N}",
            Gateway = ModelGateway.OpenRouter,
            IsActive = isActive
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<Guid>> ActiveSeatIdsAsync(Guid familyId)
    {
        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();

        return await db.Models.AsNoTracking()
            .Where(m => m.ModelFamilyId == familyId && m.IsActive && m.Gateway == Model.LabSeatGateway)
            .Select(m => m.Id)
            .ToListAsync();
    }
}
