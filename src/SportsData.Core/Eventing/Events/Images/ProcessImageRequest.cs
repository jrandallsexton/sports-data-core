using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Images;

public record ProcessImageRequest(
    Uri Url,
    Guid ImageId,
    Guid ParentEntityId,
    string Name,
    Uri? Ref,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    long Height,
    long Width,
    List<string>? Rel,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(Ref, Sport, SeasonYear, CorrelationId, CausationId)
{
    public List<string> Rel { get; init; } = Rel ?? new();
}