using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class ConferenceLogoDto(
        Guid conferenceId,
        string url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid ConferenceId { get; init; } = conferenceId;
    }
}
