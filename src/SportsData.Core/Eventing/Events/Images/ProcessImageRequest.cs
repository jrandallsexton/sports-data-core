using SportsData.Core.Common;

using System;
using System.Collections.Generic;

namespace SportsData.Core.Eventing.Events.Images
{
    public class ProcessImageRequest : EventBase
    {
        public ProcessImageRequest(
            string url,
            Guid imageId,
            Guid parentEntityId,
            string name,
            Sport sport,
            int? seasonYear,
            DocumentType documentType,
            SourceDataProvider sourceDataProvider,
            long height,
            long width,
            List<string>? rel,
            Guid correlationId,
            Guid causationId) : base(correlationId, causationId)
        {
            Url = url;
            ImageId = imageId;
            ParentEntityId = parentEntityId;
            Name = name;
            Sport = sport;
            SeasonYear = seasonYear;
            DocumentType = documentType;
            SourceDataProvider = sourceDataProvider;
            Height = height;
            Width = width;
            Rel = rel ?? [];
        }

        public string Url { get; init; }

        public Guid ImageId { get; init; }

        public Guid ParentEntityId { get; init; }

        public string Name { get; init; }

        public Sport Sport { get; init; }

        public int? SeasonYear { get; init; }

        public DocumentType DocumentType { get; init; }

        public SourceDataProvider SourceDataProvider { get; init; }

        public long Height { get; init; }

        public long Width { get; init; }

        public List<string>? Rel { get; init; }
    }
}
