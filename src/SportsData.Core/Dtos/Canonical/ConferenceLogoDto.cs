﻿using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class ConferenceLogoDto(
        Guid conferenceId,
        Uri url,
        int? height,
        int? width) : LogoDtoBase(url, height, width)
    {
        public Guid ConferenceId { get; init; } = conferenceId;
    }
}
