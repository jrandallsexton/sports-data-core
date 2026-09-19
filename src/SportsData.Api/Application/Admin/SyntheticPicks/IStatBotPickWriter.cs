using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

/// <summary>
/// Writes StatBot's pick - the pick the matchup preview dialog would make -
/// into every league that carries a contest. StatBot's picks are ordinary
/// <c>UserPick</c> rows, one per (league, contest), exactly like a real
/// member's; scoring, standings and reveal all read them the same way.
/// </summary>
public interface IStatBotPickWriter
{
    /// <summary>The synthetic user whose picks mirror the preview dialog.</summary>
    static readonly Guid StatBotUserId = Guid.Parse("5fa4c116-1993-4f2b-9729-c50c62150813");

    /// <summary>
    /// Re-derive StatBot's pick for one contest from its latest non-rejected
    /// preview, in every league whose slate contains the contest. Inserts
    /// when missing, updates when the preview now names a different winner,
    /// and never touches a contest that has already kicked off. Returns the
    /// number of picks written.
    /// </summary>
    Task<int> UpsertForContestAsync(Guid contestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Backfill: insert StatBot's missing picks for every matchup of a
    /// season-week across all leagues. Never updates an existing pick. A
    /// contest that has kicked off is filled only when its preview predates
    /// the kickoff - StatBot's record must be made of picks that were
    /// makeable at the time. Returns the number of picks inserted.
    /// </summary>
    Task<int> BackfillWeekAsync(int seasonYear, int week, CancellationToken cancellationToken = default);
}
