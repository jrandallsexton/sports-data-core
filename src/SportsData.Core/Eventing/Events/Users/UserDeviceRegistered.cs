using System;

using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Emitted by the API when a mobile client registers (or refreshes) its
    /// FCM device token — typically at sign-in or on token rotation. The API
    /// owns identity resolution: it maps the authenticated Firebase token to
    /// the internal <see cref="UserId"/> before publishing, so the
    /// Notification service can project the device into its local
    /// <c>UserDevices</c> table without taking on any auth / identity
    /// responsibility of its own. Mirrors the existing API → Notification
    /// projection pattern (UserDataPublished, PickemGroupDataPublished).
    ///
    /// <para>
    /// Consumer must be idempotent. At-least-once delivery means the same
    /// registration may arrive twice, and re-registration on every app launch
    /// / token refresh republishes it. <see cref="InstallationId"/> is the
    /// stable per-install device identifier (survives FCM token rotation and
    /// account switches); the consumer upserts on it so a device has exactly
    /// one current owner — re-registration by a different user reassigns the
    /// device rather than creating a second row pointing at the same token.
    /// </para>
    /// </summary>
    public record UserDeviceRegistered(
        Guid UserId,
        string InstallationId,
        string FcmToken,
        string Platform,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(null, Sport.All, null, CorrelationId, CausationId);
}
