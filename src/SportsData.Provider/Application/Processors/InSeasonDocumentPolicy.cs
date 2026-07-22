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
    /// PoC allow-list = <see cref="DocumentType.EventCompetitionPlay"/> only —
    /// that captures effectively all of the bleeding at near-zero risk. Expand
    /// deliberately (drives, per-game roster) once proven.
    ///
    /// See docs/features/in-season-cache-bypass-fix.md.
    /// </summary>
    public static class InSeasonDocumentPolicy
    {
        private static readonly HashSet<DocumentType> ImmutableInSeasonTypes = new()
        {
            DocumentType.EventCompetitionPlay,
        };

        public static bool IsImmutableInSeason(DocumentType documentType)
            => ImmutableInSeasonTypes.Contains(documentType);
    }
}
