using SportsData.Core.Common;
using SportsData.Provider.Application.Jobs;

namespace SportsData.Provider.Application.ResourceIndex;

public record CreateResourceIndexCommand
{
    public Sport Sport { get; init; }
    public SourceDataProvider SourceDataProvider { get; init; }
    public DocumentType DocumentType { get; init; }
    public int? SeasonYear { get; init; }
    public required Uri Ref { get; init; }
    public bool IsRecurring { get; set; }
    public string? CronExpression { get; set; }
    public bool IsEnabled { get; set; }
    public ResourceShape Shape { get; set; } = ResourceShape.Auto;

    public int? Ordinal { get; set; }
}