using System;

namespace SportsData.Core.Eventing.Events.PickemGroups;

public record PickemGroupWeekMatchupsGenerated(
    Guid GroupId,
    int SeasonYear,
    int WeekNumber,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId);