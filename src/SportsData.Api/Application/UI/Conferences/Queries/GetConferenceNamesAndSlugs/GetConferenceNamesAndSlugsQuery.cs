namespace SportsData.Api.Application.UI.Conferences.Queries.GetConferenceNamesAndSlugs;

public class GetConferenceNamesAndSlugsQuery
{
    /// <summary>
    /// The season year to get conferences for. Defaults to current year if not specified.
    /// </summary>
    public int? SeasonYear { get; init; }
}
