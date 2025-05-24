using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents
{
    public class DocumentSourcingStarted(
        Sport sport,
        int? seasonYear,
        DocumentType documentType,
        int estimatedDocumentCount,
        Guid correlationId,
        Guid causationId) : EventBase(correlationId, causationId)
    {
        public Sport Sport { get; init; } = sport;

        public int? SeasonYear { get; init; } = seasonYear;

        public DocumentType DocumentType { get; init; } = documentType;

        public int EstimatedDocumentCount { get; init; } = estimatedDocumentCount;

    }
}