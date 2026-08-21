namespace SportsData.Api.Application.Athletes.Queries.GetAthleteDetails;

/// <summary>GUID-keyed — athlete slugs are not unique (~15% collide),
/// unlike the franchise slugs the rest of the sport routes key on.</summary>
public record GetAthleteDetailsQuery(
    string Sport,
    string League,
    Guid AthleteId);
