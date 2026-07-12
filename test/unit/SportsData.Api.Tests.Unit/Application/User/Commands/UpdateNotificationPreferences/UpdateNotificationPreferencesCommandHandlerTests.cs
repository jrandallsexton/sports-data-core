using FluentAssertions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Moq;

using SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

using Xunit;

using PrefsEntity = SportsData.Api.Infrastructure.Data.Entities.UserNotificationPreferences;
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpdateNotificationPreferences;

public class UpdateNotificationPreferencesCommandHandlerTests
    : ApiTestBase<UpdateNotificationPreferencesCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    public UpdateNotificationPreferencesCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IValidator<UpdateNotificationPreferencesCommand>>(
            new UpdateNotificationPreferencesCommandValidator());
    }

    // Single source for the User required-field shape, shared by SeedUserAsync
    // (ApiTestBase's DataContext) and the race test's sidecar seed (its own options).
    private static UserEntity NewUser(Guid id) => new()
    {
        Id = id,
        FirebaseUid = $"uid-{id:N}",
        Email = "real@person.com",
        SignInProvider = "apple.com",
        DisplayName = "Real Person",
        Username = $"user{id:N}"[..12]
    };

    private async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(id));
        await DataContext.SaveChangesAsync();
        return id;
    }

    // A deliberately asymmetric flag pattern (T F T T F T F T). Because no two
    // adjacent flags share a value in the same way, a mis-mapped line in the
    // handler's Apply() (e.g. copying command.ScheduleChange into
    // OddsChangedEnabled) flips at least one assertion.
    private static UpdateNotificationPreferencesCommand DistinctFlags()
        => new()
        {
            PickResultEnabled = true,
            PickDeadlineReminderEnabled = false,
            ContestStartReminderEnabled = true,
            LeagueInviteEnabled = true,
            MembershipEnabled = false,
            MatchupPreviewEnabled = true,
            ScheduleChangeEnabled = false,
            OddsChangedEnabled = true
        };

    // Assert all eight flags on the persisted entity match the command.
    private static void AssertFlags(PrefsEntity prefs, UpdateNotificationPreferencesCommand c)
    {
        prefs.PickResultEnabled.Should().Be(c.PickResultEnabled);
        prefs.PickDeadlineReminderEnabled.Should().Be(c.PickDeadlineReminderEnabled);
        prefs.ContestStartReminderEnabled.Should().Be(c.ContestStartReminderEnabled);
        prefs.LeagueInviteEnabled.Should().Be(c.LeagueInviteEnabled);
        prefs.MembershipEnabled.Should().Be(c.MembershipEnabled);
        prefs.MatchupPreviewEnabled.Should().Be(c.MatchupPreviewEnabled);
        prefs.ScheduleChangeEnabled.Should().Be(c.ScheduleChangeEnabled);
        prefs.OddsChangedEnabled.Should().Be(c.OddsChangedEnabled);
    }

    // All eight flags on the published event match the command (plus UserId).
    private static bool EventMatches(
        UserNotificationPreferencesUpdated e, Guid userId, UpdateNotificationPreferencesCommand c)
        => e.UserId == userId
           && e.PickResultEnabled == c.PickResultEnabled
           && e.PickDeadlineReminderEnabled == c.PickDeadlineReminderEnabled
           && e.ContestStartReminderEnabled == c.ContestStartReminderEnabled
           && e.LeagueInviteEnabled == c.LeagueInviteEnabled
           && e.MembershipEnabled == c.MembershipEnabled
           && e.MatchupPreviewEnabled == c.MatchupPreviewEnabled
           && e.ScheduleChangeEnabled == c.ScheduleChangeEnabled
           && e.OddsChangedEnabled == c.OddsChangedEnabled;

    // The event was published exactly once with the full flag set (payload-specific),
    // AND exactly one UserNotificationPreferencesUpdated was published in total. The
    // second Verify is the count guard: the payload-specific one alone would pass even
    // if a second event with a different payload were also published.
    private void VerifyPublishedOnce(Guid userId, UpdateNotificationPreferencesCommand command)
    {
        var bus = Mocker.GetMock<IEventBus>();
        bus.Verify(
            b => b.Publish(
                It.Is<UserNotificationPreferencesUpdated>(e => EventMatches(e, userId, command)),
                It.IsAny<CancellationToken>()),
            Times.Once());
        bus.Verify(
            b => b.Publish(It.IsAny<UserNotificationPreferencesUpdated>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Execute_CreatesRow_WhenNoneExists_AndPublishes()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = DistinctFlags();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        // Event carries the complete flag set + userId, published exactly once.
        VerifyPublishedOnce(userId, command);

        DataContext.ChangeTracker.Clear();
        var prefs = DataContext.UserNotificationPreferences.Single(p => p.UserId == userId);
        AssertFlags(prefs, command);
        prefs.CreatedUtc.Should().Be(FixedNow);
        prefs.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Execute_UpdatesExistingRow_InPlace()
    {
        var userId = await SeedUserAsync();
        await DataContext.UserNotificationPreferences.AddAsync(new PrefsEntity
        {
            UserId = userId,
            CreatedUtc = FixedNow.AddDays(-3),
            CreatedBy = userId
            // all flags default true
        });
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = DistinctFlags();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        // Update path still publishes the complete flag set exactly once.
        VerifyPublishedOnce(userId, command);

        DataContext.ChangeTracker.Clear();
        var rows = DataContext.UserNotificationPreferences.Where(p => p.UserId == userId).ToList();
        rows.Should().HaveCount(1); // updated, not duplicated
        var prefs = rows.Single();
        AssertFlags(prefs, command);
        prefs.ModifiedUtc.Should().Be(FixedNow);
        prefs.ModifiedBy.Should().Be(userId);
    }

    // Full field-mapping verification. The first three vectors' per-field columns
    // are the distinct 3-bit codes 000..111, so every pair of fields differs in at
    // least one vector — any swapped or copied same-valued mapping in Apply() or
    // the published event is exposed. The all-false vector catches a field left at
    // its true entity-default (never assigned) on the insert path.
    [Theory]
    [InlineData(false, false, false, false, true,  true,  true,  true)]
    [InlineData(false, false, true,  true,  false, false, true,  true)]
    [InlineData(false, true,  false, true,  false, true,  false, true)]
    [InlineData(false, false, false, false, false, false, false, false)]
    public async Task Execute_MapsEveryFlag_ToEntityAndEvent(
        bool pickResult, bool pickDeadline, bool contestStart, bool leagueInvite,
        bool membership, bool matchupPreview, bool scheduleChange, bool oddsChanged)
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = new UpdateNotificationPreferencesCommand
        {
            PickResultEnabled = pickResult,
            PickDeadlineReminderEnabled = pickDeadline,
            ContestStartReminderEnabled = contestStart,
            LeagueInviteEnabled = leagueInvite,
            MembershipEnabled = membership,
            MatchupPreviewEnabled = matchupPreview,
            ScheduleChangeEnabled = scheduleChange,
            OddsChangedEnabled = oddsChanged
        };

        var result = await handler.ExecuteAsync(userId, command);
        result.IsSuccess.Should().BeTrue();

        VerifyPublishedOnce(userId, command);

        DataContext.ChangeTracker.Clear();
        var prefs = DataContext.UserNotificationPreferences.Single(p => p.UserId == userId);
        AssertFlags(prefs, command);
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid(), new UpdateNotificationPreferencesCommand());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserNotificationPreferencesUpdated>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Execute_RecoversFromConcurrentInsert_PersistsOneRow_AndPublishesOnce()
    {
        // Deterministically exercise the unique-constraint recovery path: on the
        // handler's first SaveChanges (its insert), a "winner" row is inserted via a
        // sidecar context — so the post-catch re-query finds it — and then a
        // unique-violation DbUpdateException is thrown. The handler must detach its
        // orphan, re-query the winner, apply the flags, and retry as an update.
        // The InMemory provider can't raise a real 23505, so we drive it through a
        // throwing AppDataContext (matched via the extension's string fallback).
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var userId = Guid.NewGuid();
        await using (var seed = new AppDataContext(options))
        {
            seed.Users.Add(NewUser(userId));
            await seed.SaveChangesAsync();
        }

        // Swap the handler's DbContext for the throwing one (overrides ApiTestBase's
        // registration) and point it at the same store the assertions read from.
        await using var raceContext = new RaceOnFirstInsertDataContext(options, userId, FixedNow);
        Mocker.Use<AppDataContext>(raceContext);

        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();
        var command = DistinctFlags();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();
        raceContext.ThrewOnce.Should().BeTrue("the recovery path must have been exercised");

        // Published exactly once — the retry must not re-publish.
        VerifyPublishedOnce(userId, command);

        // Exactly one row survived (the winner), carrying the requested flags.
        await using var verify = new AppDataContext(options);
        var rows = await verify.UserNotificationPreferences
            .Where(p => p.UserId == userId).ToListAsync();
        rows.Should().HaveCount(1);
        AssertFlags(rows.Single(), command);
    }

    /// <summary>
    /// AppDataContext whose first <see cref="SaveChangesAsync"/> simulates losing a
    /// concurrent first-insert race: it inserts a competing "winner" row (via a
    /// sidecar context sharing the same InMemory store) and then throws a
    /// unique-violation <see cref="DbUpdateException"/>. Later saves delegate to the
    /// base so the handler's retry-as-update succeeds.
    /// </summary>
    private sealed class RaceOnFirstInsertDataContext : AppDataContext
    {
        private readonly DbContextOptions<AppDataContext> _options;
        private readonly Guid _userId;
        private readonly DateTime _createdUtc;
        private bool _thrown;

        public RaceOnFirstInsertDataContext(
            DbContextOptions<AppDataContext> options, Guid userId, DateTime createdUtc)
            : base(options)
        {
            _options = options;
            _userId = userId;
            _createdUtc = createdUtc;
        }

        public bool ThrewOnce => _thrown;

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_thrown)
            {
                _thrown = true;

                // The concurrent winner lands between our null-check and our save.
                await using (var sidecar = new AppDataContext(_options))
                {
                    sidecar.UserNotificationPreferences.Add(new PrefsEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = _userId,
                        CreatedUtc = _createdUtc,
                        CreatedBy = _userId
                    });
                    await sidecar.SaveChangesAsync(cancellationToken);
                }

                // The unique index rejects our insert. Message trips the extension's
                // string fallback (InMemory can't produce a real Npgsql 23505).
                throw new DbUpdateException(
                    "insert failed",
                    new InvalidOperationException(
                        "duplicate key value violates unique constraint \"IX_UserNotificationPreferences_UserId\""));
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}
