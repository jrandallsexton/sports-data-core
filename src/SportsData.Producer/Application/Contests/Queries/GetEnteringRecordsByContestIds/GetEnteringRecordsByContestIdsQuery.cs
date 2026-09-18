using System;

namespace SportsData.Producer.Application.Contests.Queries.GetEnteringRecordsByContestIds;

public record GetEnteringRecordsByContestIdsQuery(Guid[] ContestIds);
