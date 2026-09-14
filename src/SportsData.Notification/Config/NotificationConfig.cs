namespace SportsData.Notification.Config
{
    /// <summary>
    /// Service-level knobs, bound from AppConfig section
    /// <c>SportsData.Notification:NotificationConfig</c> (same shape as
    /// API's <c>SportsData.Api:ApiConfig</c>). Defaults here are the
    /// operative values when the section is absent (local dev, tests).
    /// </summary>
    public class NotificationConfig
    {
        /// <summary>
        /// Master switch for outbound FCM/APNs sends. False by default so a
        /// local stack carrying real Firebase credentials and a prod-restored
        /// UserDevice table cannot push to real phones; only the Prod label
        /// sets it true. When false the no-op sender is registered even if
        /// <c>CommonConfig:Firebase</c> is fully configured.
        /// </summary>
        public bool PushEnabled { get; set; } = false;
        /// <summary>
        /// Minutes before a kickoff wave's anchor that the pick-deadline
        /// reminder fires. Operator decision 2026-09-05: 60, configurable.
        /// </summary>
        public int PickDeadlineLeadMinutes { get; set; } = 60;

        /// <summary>
        /// Kickoffs within this many minutes of a wave's anchor (earliest
        /// kickoff) coalesce into that wave — one reminder covers the
        /// Saturday 16:00/16:15/16:30 stagger instead of three pushes.
        /// </summary>
        public int PickDeadlineCoalesceMinutes { get; set; } = 30;
    }
}
