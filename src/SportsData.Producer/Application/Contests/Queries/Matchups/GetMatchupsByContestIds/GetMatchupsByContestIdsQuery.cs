using System;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;

public record GetMatchupsByContestIdsQuery(
    Guid[] ContestIds,
    MarkDirection Direction);
