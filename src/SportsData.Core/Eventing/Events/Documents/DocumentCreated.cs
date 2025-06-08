using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;

using System;

namespace SportsData.Core.Eventing.Events.Documents
{
    public class DocumentCreated(
        string id,
        string? parentId,
        string name,
        string routingKey,
        string urlHash,
        Sport sport,
        int? seasonYear,
        DocumentType documentType,
        SourceDataProvider sourceDataProvider,
        Guid correlationId,
        Guid causationId)
        : EventBase(correlationId, causationId) , IHasSourceUrlHash
    {
        public string Id { get; init; } = id;

        public string? ParentId { get; set; } = parentId;

        public string RoutingKey { get; set; } = routingKey;

        public string Name { get; init; } = name;

        /// <summary>
        /// [TODO: Remove after RoutingKey routing is fully implemented]
        /// </summary>
        public Sport Sport { get; init; } = sport;

        public int? SeasonYear { get; init; } = seasonYear;

        /// <summary>
        /// [TODO: Remove after RoutingKey-based dispatch eliminates DocumentType]
        /// </summary>
        public DocumentType DocumentType { get; init; } = documentType;

        /// <summary>
        /// [TODO: Remove once SourceDataProvider is encoded in RoutingKey prefix]
        /// </summary>
        public SourceDataProvider SourceDataProvider { get; set; } = sourceDataProvider;

        public string UrlHash { get; set; } = urlHash;
    }
}
