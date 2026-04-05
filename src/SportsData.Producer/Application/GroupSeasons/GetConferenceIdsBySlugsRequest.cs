using System.Collections.Generic;

namespace SportsData.Producer.Application.GroupSeasons;

public class GetConferenceIdsBySlugsRequest
{
    public int SeasonYear { get; set; }
    public List<string> Slugs { get; set; } = [];
}
