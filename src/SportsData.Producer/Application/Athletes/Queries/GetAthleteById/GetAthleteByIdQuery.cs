namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteById;

/// <summary>Keyed by GUID, not slug — athlete slugs are not unique
/// (~15% collide across the corpus), unlike franchise slugs.</summary>
public record GetAthleteByIdQuery(Guid AthleteId);
