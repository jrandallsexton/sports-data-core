using System;

using SportsData.Core.Common;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Wire shape for POST /contests/matchups/by-ids. Carries the contest ids plus
/// the requested mark direction so Producer's SQL can prefer the matching
/// generated team-mark rows. Direction defaults to Roundel (the enum's zero
/// value) for any caller that doesn't set it explicitly.
/// </summary>
public record GetMatchupsByContestIdsRequest(
    Guid[] ContestIds,
    MarkDirection Direction);
