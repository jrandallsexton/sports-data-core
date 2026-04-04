using System;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsByContestIds;

public record GetMatchupsByContestIdsQuery(Guid[] ContestIds);
