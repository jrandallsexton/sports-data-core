using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Commands.UpdateContest
{
    /// <param name="IncludeLinkedDocumentTypes">Null = the standard
    /// "Refresh Contest" set (<see cref="DefaultIncludeLinkedDocumentTypes"/>);
    /// populated = spawn only the listed child document types, which must be
    /// a subset of the standard set (e.g. EventCompetition +
    /// EventCompetitionCompetitor to re-source competitor curated ranks
    /// without re-pulling plays, drives and probabilities).</param>
    public record UpdateContestCommand(
        Guid ContestId,
        SourceDataProvider SourceDataProvider,
        Sport Sport,
        Guid CorrelationId,
        List<DocumentType>? IncludeLinkedDocumentTypes = null)
    {
        /// <summary>
        /// Document types to source on a "Refresh Contest" request. Anything
        /// outside this set is filtered out at every spawn site downstream
        /// (via DocumentProcessorBase.ShouldSpawn). Excludes roster, per-team
        /// statistics, and athlete-level docs — those are sourced once during
        /// initial ingestion and don't change on a contest refresh.
        ///
        /// See docs/refresh-contest-cascade-narrowing.md for rationale.
        /// </summary>
        public static readonly IReadOnlyList<DocumentType> DefaultIncludeLinkedDocumentTypes =
        [
            DocumentType.Event,
            DocumentType.EventCompetition,
            DocumentType.EventCompetitionStatus,
            DocumentType.EventCompetitionSituation,
            DocumentType.EventCompetitionBroadcast,
            DocumentType.EventCompetitionOdds,
            DocumentType.EventCompetitionCompetitor,
            DocumentType.EventCompetitionCompetitorScore,
            DocumentType.EventCompetitionCompetitorLineScore,
            DocumentType.EventCompetitionCompetitorRecord,
            DocumentType.EventCompetitionPlay,
            DocumentType.EventCompetitionDrive,
            DocumentType.EventCompetitionLeaders,
            DocumentType.EventCompetitionProbability
        ];
    }
}
