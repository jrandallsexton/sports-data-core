namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteStatlines;

/// <summary>
/// Batch statline lookup for Player Pick'em scoring: every
/// (athleteSeason, contest) statline where the athlete is in
/// <see cref="AthleteSeasonIds"/> AND the contest is in
/// <see cref="ContestIds"/>. Callers pass a lineup's anchored slots in
/// ONE call.
/// </summary>
public record GetAthleteStatlinesQuery(
    IReadOnlyList<Guid> ContestIds,
    IReadOnlyList<Guid> AthleteSeasonIds);
