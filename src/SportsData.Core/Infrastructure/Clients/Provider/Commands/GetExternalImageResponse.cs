using System;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands;

public class GetExternalImageResponse
{
    public required string Id { get; set; }

    public required string CanonicalId { get; set; }

    public required Uri Uri { get; set; }

    public bool IsSuccess { get; set; } = true;
}