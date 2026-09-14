using FluentAssertions;

using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Infrastructure.Notifications;

/// <summary>
/// The gate itself. The case that matters is the first one: credentials
/// present, switch absent — that is the Local label, and it must NOT send.
/// </summary>
public class PushSenderSelectionTests
{
    [Theory]
    [InlineData("sportdeets-prod")]
    [InlineData("")]
    [InlineData(null)]
    public void Decide_PushDisabled_NeverUsesFirebase_RegardlessOfCredentials(string? projectId)
    {
        var decision = PushSenderSelection.Decide(pushEnabled: false, firebaseProjectId: projectId);

        decision.UseFirebase.Should().BeFalse();
        decision.NoOpReason.Should().Be(PushSenderSelection.DisabledByConfigReason);
    }

    [Fact]
    public void Decide_PushEnabledWithProjectId_UsesFirebase()
    {
        var decision = PushSenderSelection.Decide(pushEnabled: true, firebaseProjectId: "sportdeets-prod");

        decision.UseFirebase.Should().BeTrue();
        decision.NoOpReason.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Decide_PushEnabledWithoutProjectId_NoOpsWithNotConfiguredReason(string? projectId)
    {
        var decision = PushSenderSelection.Decide(pushEnabled: true, firebaseProjectId: projectId);

        decision.UseFirebase.Should().BeFalse();
        decision.NoOpReason.Should().Be(PushSenderSelection.FirebaseNotConfiguredReason);
    }

    [Fact]
    public void TheTwoReasons_AreDistinct_SoOperatorsCanTellThemApart()
    {
        PushSenderSelection.DisabledByConfigReason.Should().NotBe(PushSenderSelection.FirebaseNotConfiguredReason);
    }
}
