using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Notification.Application.Dispatching;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// One line of notification copy for a given voice + pick situation.
    /// Modelled on API's <c>Prompt</c> entity and for the same reason its doc
    /// gives: the database is private and the repo is PUBLIC, so the text
    /// lives here rather than in source. For SmackBot that matters twice over
    /// — the voice is the differentiator, and a user who can read every taunt
    /// in advance loses the surprise that makes it land.
    ///
    /// <para>
    /// Rows are inserted out-of-band (SQL kept outside the repo). An empty
    /// catalog is a supported state: the consumer falls back to the standard
    /// copy, so shipping the schema before the content is safe.
    /// </para>
    ///
    /// <para>
    /// DIVERGENCE FROM <c>Prompt</c>: Prompt resolves to exactly one
    /// <c>IsDefault</c> row per slot, enforced by partial unique indexes. Here
    /// we want MANY active rows per slot with one chosen at send time, so
    /// <see cref="IsActive"/> replaces IsDefault and the unique-slot indexes
    /// do not carry over.
    /// </para>
    ///
    /// See docs/features/smackbot-voice.md.
    /// </summary>
    public class SmackPhrase : CanonicalEntityBase<Guid>
    {
        /// <summary>Which voice this line belongs to.</summary>
        public NotificationVoice Voice { get; set; } = NotificationVoice.Smack;

        /// <summary>The resolution slot — what happened to the pick.</summary>
        public PickSituation Situation { get; set; }

        /// <summary>
        /// Null = applies to any sport; a sport-specific row outranks it.
        /// Mirrors Prompt's precedence rule.
        /// </summary>
        public Sport? Sport { get; set; }

        /// <summary>
        /// The line, with <c>{Token}</c> placeholders resolved at send time
        /// (see <c>SmackPhraseFormatter</c>).
        /// </summary>
        public required string Text { get; set; }

        /// <summary>
        /// Soft on/off. Retiring a line must not delete it — sends already
        /// captured its text, and an operator may want it back.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// True when the line references the betting line (spread/total).
        /// Filtered out unless the recipient's context permits gambling
        /// content — an ATS player opted into spread scoring, a StraightUp
        /// player who hid gambling content did not.
        /// </summary>
        public bool RequiresGamblingContent { get; set; }

        /// <summary>
        /// Relative selection frequency; a line with weight 3 is three times
        /// as likely as weight 1. Must be >= 1.
        /// </summary>
        public int Weight { get; set; } = 1;

        /// <summary>Optional operator note for the management UI.</summary>
        public string Description { get; set; }

        /// <summary>PostgreSQL xmin concurrency token — phrases are operator-edited.</summary>
        public uint RowVersion { get; set; }

    }

    /// <summary>
    /// The voice a user's pick-result notifications speak in. An enum rather
    /// than a bool so later voices cost no schema change.
    /// </summary>
    public enum NotificationVoice
    {
        /// <summary>The neutral scoreline copy. Default for every user.</summary>
        Standard = 0,

        /// <summary>SmackBot — taunts losses, grudgingly acknowledges wins.</summary>
        Smack = 1
    }
}
