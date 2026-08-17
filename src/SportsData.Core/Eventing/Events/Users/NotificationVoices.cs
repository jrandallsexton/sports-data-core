namespace SportsData.Core.Eventing.Events.Users
{
    /// <summary>
    /// Wire names for the pick-result notification voice. The voice travels as
    /// a STRING on events and in the API contract — same precedent as
    /// PickType on <c>PickemGroupDataPublished</c> — so Core does not take a
    /// dependency on Notification's enum and an unknown value degrades rather
    /// than failing deserialization. Notification parses to its own
    /// <c>NotificationVoice</c> with a Standard fallback.
    /// See docs/features/smackbot-voice.md.
    /// </summary>
    public static class NotificationVoices
    {
        public const string Standard = "Standard";

        public const string Smack = "Smack";

        /// <summary>Every recognised wire name — the API validator's allow-list.</summary>
        public static readonly string[] All = [Standard, Smack];

        // Ordinal (case-sensitive) by construction — the wire contract is
        // exact names, and Notification parses the same way.
        public static bool IsKnown(string? value) =>
            value is Standard or Smack;
    }
}
