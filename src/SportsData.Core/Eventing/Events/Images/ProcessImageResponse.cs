﻿using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Images;

public record ProcessImageResponse(
    Uri Uri,
    string ImageId,
    string OriginalUrlHash,
    Guid ParentEntityId,
    string Name,
    Sport Sport,
    int? SeasonYear,
    DocumentType DocumentType,
    SourceDataProvider SourceDataProvider,
    long Height,
    long Width,
    List<string>? Rel,
    Guid CorrelationId,
    Guid CausationId
) : EventBase(CorrelationId, CausationId)
{
    public List<string> Rel { get; init; } = Rel ?? new();
}