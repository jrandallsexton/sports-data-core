using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Notification.Application.Dispatching;

namespace SportsData.Notification.Infrastructure.Data.Entities
{
    /// <summary>
    /// An operator's 0–4 star rating of a previewed SmackBot line, written by
    /// the SmackBot Lab. Deliberately shaped as TRAINING DATA: the row pairs
    /// the pick facts (features, as JSON) with the emitted line and a star
    /// label, so future models can generate candidate taunts and be scored
    /// against operator taste.
    ///
    /// <para>
    /// One row per (pick, voice) — the Lab upserts, so re-rating after a
    /// phrase edit overwrites rather than duplicates. <see cref="PhraseId"/>
    /// is null when the preview fell back to standard copy; rating a fallback
    /// is allowed and useful (a low star marks a bucket that needs lines).
    /// <see cref="RenderedText"/> preserves the exact string rated, immune to
    /// later phrase edits. See docs/features/smackbot-lab.md.
    /// </para>
    /// </summary>
    public class SmackPreviewRating : CanonicalEntityBase<Guid>
    {
        /// <summary>The scored pick this rating previews (API's PickemGroupUserPick.Id).</summary>
        public Guid PickId { get; set; }

        public Guid ContestId { get; set; }

        public Guid LeagueId { get; set; }

        /// <summary>The user who made the pick — provenance, not the rater.</summary>
        public Guid PickerUserId { get; set; }

        public NotificationVoice Voice { get; set; }

        public PickSituation Situation { get; set; }

        /// <summary>Null = the preview fell back to standard copy.</summary>
        public Guid? PhraseId { get; set; }

        /// <summary>The exact line that was rated.</summary>
        public string RenderedText { get; set; }

        /// <summary>0–4 stars; range enforced by a DB check constraint.</summary>
        public int Stars { get; set; }

        /// <summary>The preview's full fact payload — the training features.</summary>
        public string FactsJson { get; set; }

        // Deliberately NO xmin concurrency token. This is a solo-operator
        // admin surface (operator decision, PR #645): the upsert's
        // unique-index race fallback is the only concurrency this needs, and
        // a token here would demand retry handling and integration coverage
        // for a conflict that cannot occur in practice.
    }
}
