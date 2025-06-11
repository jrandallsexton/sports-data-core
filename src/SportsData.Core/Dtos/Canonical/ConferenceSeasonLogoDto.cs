using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class ConferenceSeasonLogoDto(
        Guid conferenceSeasonId,
        Uri url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid ConferenceSeasonId { get; init; } = conferenceSeasonId;
    }
}
