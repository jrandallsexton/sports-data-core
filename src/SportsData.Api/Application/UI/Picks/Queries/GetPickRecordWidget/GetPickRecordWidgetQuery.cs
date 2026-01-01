namespace SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;

public class GetPickRecordWidgetQuery
{
    public required Guid UserId { get; init; }

    public required int SeasonYear { get; init; }

    public required bool ForSynthetic { get; init; }
}
