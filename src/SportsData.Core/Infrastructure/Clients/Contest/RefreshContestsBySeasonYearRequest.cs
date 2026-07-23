using System;

using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Contest;

/// <summary>
/// POST body for Producer's <c>contests/refresh</c> endpoint. The property
/// shape mirrors Producer's <c>RefreshContestsBySeasonYearCommand</c> so the
/// <c>[FromBody]</c> bind succeeds. <see cref="Sport"/> is echoed even though
/// the client already routed to the matching per-sport Producer pod — Producer
/// guards it against its own <c>CurrentSport</c>. <see cref="CorrelationId"/>
/// threads the API trace id through so the same id appears in Producer's Seq
/// logs. See docs/features/season-contest-resource-driver.md.
/// </summary>
public record RefreshContestsBySeasonYearRequest(
    Sport Sport,
    int SeasonYear,
    Guid CorrelationId);
