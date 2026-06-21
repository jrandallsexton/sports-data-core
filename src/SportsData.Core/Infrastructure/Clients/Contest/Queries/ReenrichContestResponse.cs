using System;

namespace SportsData.Core.Infrastructure.Clients.Contest.Queries;

/// <summary>
/// Producer's response to the admin re-enrich endpoint. The CorrelationId
/// is the value Producer logged the work under (and the value the
/// downstream ContestFinalized broadcast carries) — surfaced to the admin
/// UI so an operator can paste it into Seq for tracing.
/// </summary>
public record ReenrichContestResponse(Guid CorrelationId, Guid ContestId);
