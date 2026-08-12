using System.Collections.Generic;

using SportsData.Core.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;

/// <param name="IncludeLinkedDocumentTypes">Null/empty = full TeamSeason
/// child cascade (historical behavior); populated = the TeamSeason
/// processor spawns only the listed child document types.</param>
public record RequestFranchiseSeasonSourcingCommand(
    int SeasonYear,
    Sport Sport,
    List<DocumentType>? IncludeLinkedDocumentTypes = null);
