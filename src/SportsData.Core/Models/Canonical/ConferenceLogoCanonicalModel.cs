using System;

namespace SportsData.Core.Models.Canonical
{
    public class ConferenceLogoCanonicalModel(
        Guid conferenceId,
        string url,
        int? height,
        int? width) : CanonicalLogoModelBase(url, height, width)
    {
        public Guid ConferenceId { get; init; } = conferenceId;
    }
}
