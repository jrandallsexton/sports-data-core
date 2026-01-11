using System;

namespace SportsData.Core.Infrastructure.Refs;

/// <summary>
/// Generates absolute URIs for resources across all microservices.
/// Supports HATEOAS links in DTOs and integration events.
/// Configured per-sport at startup via IAppMode.
/// </summary>
public interface IGenerateResourceRefs
{
    // Producer resources
    Uri ForCompetition(Guid competitionId);
    Uri ForFranchiseSeason(Guid franchiseSeasonId);
    Uri ForAthlete(Guid athleteId);
    Uri ForAthleteSeason(Guid athleteSeasonId);
    Uri ForCoach(Guid coachId);
    Uri ForSeason(Guid seasonId);
    Uri ForSeasonPhase(Guid seasonPhaseId);
    Uri ForSeasonWeek(Guid seasonWeekId);

    // API/Contest resources
    Uri ForContest(Guid contestId);
    Uri ForPick(Guid pickId);
    Uri ForRanking(int seasonYear);
    Uri ForMatchupPreview(Guid contestId);

    // Venue resources
    Uri ForVenue(Guid venueId);
    Uri ForVenues(int? pageNumber = null, int? pageSize = null);
    
    // Franchise resources
    Uri ForFranchise(Guid franchiseId);
    Uri ForFranchises(int? pageNumber = null, int? pageSize = null);
}
