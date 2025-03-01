﻿using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Images
{
    public class ProcessImageRequest(
        string url,
        Guid imageId,
        Guid parentEntityId,
        string imageName,
        Sport sport,
        int? seasonYear,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider,
        long height,
        long width,
        List<string>? rel,
        Guid correlationId,
        Guid causationId)
        : EventBase(correlationId, causationId)
    {
        public string Url { get; init; } = url;

        public Guid ImageId { get; init; } = imageId;

        public Guid ParentEntityId { get; init; } = parentEntityId;

        public string Name { get; init; } = imageName;

        public Sport Sport { get; init; } = sport;

        public int? SeasonYear { get; init; } = seasonYear;

        public DocumentType DocumentType { get; init; } = documentType;

        public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

        public long Height { get; init; } = height;

        public long Width { get; init; } = width;

        public List<string>? Rel { get; init; } = rel;
    }
}
