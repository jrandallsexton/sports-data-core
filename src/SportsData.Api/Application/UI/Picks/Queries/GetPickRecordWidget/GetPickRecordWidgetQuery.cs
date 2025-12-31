namespace SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;

public class GetPickRecordWidgetQuery
{
    public Guid UserId { get; set; }

    public int SeasonYear { get; set; }

    public bool ForSynthetic { get; set; }
}
