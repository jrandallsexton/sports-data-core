using SportsData.Core.Common;

namespace SportsData.Provider.Application.Processors
{
    /// <summary>
    /// Classifies whether a document type is <b>immutable once created</b>, for
    /// in-season cache decisions.
    ///
    /// Cache-bypass is otherwise decided per-season (<c>ShouldBypassCache</c>):
    /// anything in the current season is re-fetched from ESPN so live-mutable
    /// data (status, situation, score, odds, ...) stays fresh. But that is the
    /// wrong axis for an individual immutable item: a completed play never
    /// changes, yet the live streamer re-pages the play index every 30s and
    /// re-fetches every play from ESPN each cycle — saturating ESPN's 403 rate
    /// limiter and stalling live finalization.
    ///
    /// Immutable-in-season types should be served from Mongo even during the
    /// current season (except the live edge — the newest, still-finalizing item,
    /// handled at the enqueue site). Mutable aggregates must NOT be listed here.
    ///
    /// Allow-list started as <see cref="DocumentType.EventCompetitionPlay"/> only
    /// (the PoC). <see cref="DocumentType.EventCompetitionProbability"/> added
    /// 2026-09-07: ESPN emits one probability item per play — the index is
    /// append-only and completed entries never change, and its exclusion sent
    /// every item to ESPN each 15s live cycle (611,931 processed in one evening
    /// for ~500 real items).
    /// <see cref="DocumentType.EventCompetitionDrive"/> added 2026-09-08 after
    /// the plays/probs fix VERIFIED in prod (233x/900x reductions) left drives
    /// as the #1 remaining amplifier (36,276 processed for one game's ~25
    /// drives): the drives index is append-only and only the ACTIVE drive
    /// mutates (plays append to it) — and the active drive is the newest item,
    /// i.e. exactly the live edge the fan-out already re-fetches every cycle.
    /// Completed drives share plays' accepted correction gap (a rare late edit
    /// to an old drive is not re-fetched until reenrich). Expand further
    /// (per-game roster) deliberately, once proven.
    ///
    /// See docs/features/in-season-cache-bypass-fix.md and
    /// docs/features/live-sourcing-already-seen-skip.md.
    /// </summary>
    public static class InSeasonDocumentPolicy
    {
        private static readonly HashSet<DocumentType> ImmutableInSeasonTypes = new()
        {
            DocumentType.EventCompetitionPlay,
            DocumentType.EventCompetitionProbability,
            DocumentType.EventCompetitionDrive,
        };

        public static bool IsImmutableInSeason(DocumentType documentType)
            => ImmutableInSeasonTypes.Contains(documentType);
    }
}
