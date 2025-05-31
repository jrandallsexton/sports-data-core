using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents
{
    public class DocumentUpdated(string id,
        string name,
        string routingKey,
        string urlHash,
        Sport sport,
        int ? seasonYear,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider,
        Guid correlationId,
        Guid causationId) :
        DocumentCreated(id, name, routingKey, urlHash, sport, seasonYear,
            documentType, sourceDataProvider, correlationId, causationId) { }
}
