using System;

namespace SportsData.Core.Dtos.Canonical;

/// <summary>
/// Batch request for entering records. POST rather than GET because the id
/// list routinely exceeds what a query string should carry.
/// </summary>
public record GetEnteringRecordsByContestIdsRequest(Guid[] ContestIds);
