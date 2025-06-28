using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.Provider.Commands;

public class ProcessResourceIndexCommand
{
    public SourceDataProvider SourceDataProvider { get; set; }

    public Sport Sport { get; set; }

    public DocumentType DocumentType { get; set; }

    public int? Season { get; set; }

    public required string ResourceIndexUrl { get; set; }
}