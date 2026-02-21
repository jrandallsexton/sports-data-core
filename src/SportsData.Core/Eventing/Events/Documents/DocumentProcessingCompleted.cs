using SportsData.Core.Common;

using System;

namespace SportsData.Core.Eventing.Events.Documents;

/// <summary>
/// Published by Producer after successfully processing a document that has NotifyOnCompletion = true.
/// Consumed by Provider saga to orchestrate tier progression in historical sourcing runs.
/// </summary>
public record DocumentProcessingCompleted(
    Guid CorrelationId,
    DocumentType DocumentType,
    string SourceUrlHash,
    DateTimeOffset CompletedUtc,
    Sport Sport,
    int? SeasonYear
);
