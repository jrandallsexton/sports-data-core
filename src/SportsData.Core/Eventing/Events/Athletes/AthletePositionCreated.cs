﻿using SportsData.Core.Dtos.Canonical;

using System;

namespace SportsData.Core.Eventing.Events.Athletes
{
    public record AthletePositionCreated(
        AthletePositionDto Canonical,
        Guid CorrelationId,
        Guid CausationId
    ) : EventBase(CorrelationId, CausationId);
}