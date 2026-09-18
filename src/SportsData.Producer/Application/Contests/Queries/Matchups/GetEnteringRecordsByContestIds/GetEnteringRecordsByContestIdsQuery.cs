using System;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetEnteringRecordsByContestIds;

public record GetEnteringRecordsByContestIdsQuery(Guid[] ContestIds);
