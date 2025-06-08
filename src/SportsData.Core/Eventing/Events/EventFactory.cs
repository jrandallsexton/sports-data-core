using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SportsData.Core.Eventing.Events
{
    public static class EventFactory
    {
        public static List<ProcessImageRequest> CreateProcessImageRequests(
            List<EspnImageDto> images,
            Guid parentId,
            Sport sport,
            int? season,
            DocumentType documentType,
            SourceDataProvider provider,
            Guid correlationId,
            Guid causationId)
        {
            return images
                .Select((img, index) => new ProcessImageRequest(
                    url: img.Href?.ToString() ?? string.Empty,
                    imageId: Guid.NewGuid(),
                    parentEntityId: parentId,
                    name: $"{parentId}-{index}.png",
                    sport: sport,
                    seasonYear: season,
                    documentType: documentType,
                    sourceDataProvider: provider,
                    height: img.Height ?? 0,
                    width: img.Width ?? 0,
                    rel: img.Rel,
                    correlationId: correlationId,
                    causationId: causationId))
                .ToList();
        }
    }
}
