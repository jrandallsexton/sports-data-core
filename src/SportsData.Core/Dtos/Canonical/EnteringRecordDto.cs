using System;

namespace SportsData.Core.Dtos.Canonical
{
    /// <summary>
    /// The record each team carried INTO a contest, derived from outcomes:
    /// wins and losses across that franchise season's prior finalized,
    /// non-preseason contests.
    /// </summary>
    /// <remarks>
    /// Point-in-time by construction, which the two obvious alternatives are
    /// not. <c>FranchiseSeason.Wins</c> is mutable and CURRENT, so it answers
    /// "what is this team's record now" — right only for the live week.
    /// <c>CompetitionCompetitorRecord.Summary</c> is ESPN's own record string,
    /// which means "entering" before a game and "after" once re-sourced
    /// post-game, with nothing marking which (prod 2026-09-18: 20 of 32 NFL
    /// week-1 rows still held their pre-game 0-0 while 6 had been refreshed).
    /// <para>
    /// Only contests carrying <c>FinalizedUtc</c> are counted, so a team with
    /// a game stuck mid-audit reads one game light until that settles. The
    /// consuming audit is idempotent precisely so it can be re-run afterwards.
    /// </para>
    /// </remarks>
    public record EnteringRecordDto
    {
        public Guid ContestId { get; init; }

        public Guid AwayFranchiseSeasonId { get; init; }

        public Guid HomeFranchiseSeasonId { get; init; }

        public int AwayWins { get; init; }

        public int AwayLosses { get; init; }

        public int AwayConferenceWins { get; init; }

        public int AwayConferenceLosses { get; init; }

        public int HomeWins { get; init; }

        public int HomeLosses { get; init; }

        public int HomeConferenceWins { get; init; }

        public int HomeConferenceLosses { get; init; }
    }
}
