using System;

namespace SportsData.Core.Models.Canonical
{
    public class ConferenceSeasonLogoCanonicalModel(
        Guid conferenceSeasonId,
        string url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid ConferenceSeasonId { get; init; } = conferenceSeasonId;
    }
}
