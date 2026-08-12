using System.Collections.Generic;

using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Franchise;

/// <summary>
/// Optional narrowing for a franchise-season sourcing request. Null/empty
/// IncludeLinkedDocumentTypes preserves the historical behavior: the full
/// TeamSeason child cascade (logos, records, ranks, stats, events, ...).
/// A populated list restricts the TeamSeason processor to spawning only
/// the listed child document types — e.g. [TeamSeasonRecord] re-sources
/// W/L records across a season without re-cascading everything else.
/// </summary>
public class FranchiseSeasonSourcingRequest
{
    public List<DocumentType>? IncludeLinkedDocumentTypes { get; set; }
}
