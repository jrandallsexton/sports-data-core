namespace SportsData.Notification.Infrastructure.Notifications;

/// <summary>
/// Decides which <see cref="IPushNotificationSender"/> Program.cs registers.
/// Pure so the gate can be unit-tested: outbound push is OFF unless
/// <c>SportsData.Notification:NotificationConfig:PushEnabled</c> is true, and
/// credentials alone are never the switch — the Local label carries real
/// Firebase credentials and a prod-restored UserDevice table.
/// </summary>
public static class PushSenderSelection
{
    public const string DisabledByConfigReason =
        "push disabled by config (SportsData.Notification:NotificationConfig:PushEnabled is false)";

    public const string FirebaseNotConfiguredReason =
        "Firebase not configured (CommonConfig:Firebase:ProjectId is not set)";

    public sealed record Decision(bool UseFirebase, string? NoOpReason);

    public static Decision Decide(bool pushEnabled, string? firebaseProjectId)
    {
        if (!pushEnabled)
            return new Decision(UseFirebase: false, NoOpReason: DisabledByConfigReason);

        if (string.IsNullOrWhiteSpace(firebaseProjectId))
            return new Decision(UseFirebase: false, NoOpReason: FirebaseNotConfiguredReason);

        return new Decision(UseFirebase: true, NoOpReason: null);
    }
}
