using System;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands;

public class GetExternalDocumentResponse
{
    public required string Id { get; set; }

    public string? CanonicalId { get; set; }

    public required Uri Uri { get; set; }

    public required string Data { get; set; }

    public bool IsSuccess { get; set; } = true;
}