using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
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
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            List<EspnImageDto> images,
            Guid parentId,
            Sport sport,
            int? season,
            DocumentType documentType,
            SourceDataProvider provider,
            Guid correlationId,
            Guid causationId)
        {
            return images.Select((img, index) =>
                new ProcessImageRequest(
                    img.Href,                             // Uri
                    externalRefIdentityGenerator.Generate(img.Href).CanonicalId, // ImageId
                    parentId,                             // ParentEntityId
                    $"{parentId}-{index}.png",            // Name
                    null,                                 // Ref
                    sport,                                // Sport
                    season,                               // SeasonYear
                    documentType,                         // DocumentType
                    provider,                             // SourceDataProvider
                    img.Height ?? 0,                      // Height
                    img.Width ?? 0,                       // Width
                    img.Rel,                              // Rel
                    correlationId,                        // CorrelationId
                    causationId                           // CausationId
                )
            ).ToList();
        }
    }
}