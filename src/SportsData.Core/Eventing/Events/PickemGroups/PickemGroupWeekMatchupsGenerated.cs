using System;
using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.PickemGroups;

public record PickemGroupWeekMatchupsGenerated(
    Guid GroupId,
    int WeekNumber,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId);