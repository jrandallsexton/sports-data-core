using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Images
{
    public class ProcessImageResponse(
        string url,
        string imageId,
        Guid parentEntityId,
        string name,
        Sport sport,
        int? seasonYear,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider,
        long height,
        long width,
        List<string>? rel) : EventBase
    {
        public string Url { get; set; } = url;

        public string ImageId { get; init; } = imageId;

        public Guid ParentEntityId { get; set; } = parentEntityId;

        public string Name { get; init; } = name;

        public Sport Sport { get; init; } = sport;

        public int? SeasonYear { get; init; } = seasonYear;

        public DocumentType DocumentType { get; init; } = documentType;

        public SourceDataProvider SourceDataProvider { get; init; } = sourceDataProvider;

        public long Height { get; init; } = height;

        public long Width { get; init; } = width;

        public List<string>? Rel { get; init; } = rel;
    }
}
