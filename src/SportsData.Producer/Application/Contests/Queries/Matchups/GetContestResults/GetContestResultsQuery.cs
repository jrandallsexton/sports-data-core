using System;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestResults;

public record GetContestResultsByContestIdsQuery(Guid[] ContestIds);
