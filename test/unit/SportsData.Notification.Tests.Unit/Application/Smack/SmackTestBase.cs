using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Tests.Unit.Application.Smack;

/// <summary>
/// Shared seeding + fixture time for the Smack slice's handler tests. The
/// Lab's contract is FIDELITY: a preview must run the send path's exact
/// resolution so a rating grades what a user would actually have received —
/// preview tests use the real catalog (seeded DbContext), not a mock.
/// </summary>
public abstract class SmackTestBase<T> : NotificationTestBase<T> where T : class
{
    protected static readonly DateTime FixedNow = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    protected SmackTestBase()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
    }

    protected async Task<SmackPhrase> SeedPhraseAsync(
        PickSituation situation,
        string text,
        bool gambling = false,
        bool active = true)
    {
        var phrase = new SmackPhrase
        {
            Id = Guid.NewGuid(),
            Voice = NotificationVoice.Smack,
            Situation = situation,
            Text = text,
            IsActive = active,
            RequiresGamblingContent = gambling,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };
        DataContext.SmackPhrases.Add(phrase);
        await DataContext.SaveChangesAsync();
        return phrase;
    }

    /// <summary>Straight-up blowout loss (7-35, away pick): BlowoutLoss.</summary>
    protected static SmackPreviewPickDto BlowoutLossPick(Guid? pickId = null) => new()
    {
        PickId = pickId ?? Guid.NewGuid(),
        ContestId = Guid.NewGuid(),
        LeagueId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        AwayAbbreviation = "AWY",
        HomeAbbreviation = "HOM",
        AwayScore = 7,
        HomeScore = 35,
        IsCorrect = false,
        PickedIsHome = false,
        LeagueName = "Sluggers",
        Sport = Sport.FootballNcaa
    };
}
