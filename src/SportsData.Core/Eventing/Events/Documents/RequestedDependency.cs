using SportsData.Core.Common;

namespace SportsData.Core.Eventing.Events.Documents;

/// <summary>
/// Represents a dependency that has been requested during document processing.
/// Uses a record type instead of a tuple to ensure field names (Type, UrlHash) 
/// survive serialization through MassTransit/System.Text.Json.
/// </summary>
/// <param name="Type">The type of document that was requested.</param>
/// <param name="UrlHash">The URL hash identifying the specific document instance.</param>
public record RequestedDependency(DocumentType Type, string UrlHash);
