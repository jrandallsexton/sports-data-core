using System.Collections.Generic;

namespace SportsData.Producer.Application.GroupSeasons.Queries.GetConferenceIdsBySlugs;

public record GetConferenceIdsBySlugsQuery(int SeasonYear, List<string> Slugs);
