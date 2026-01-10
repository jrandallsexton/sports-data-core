using Microsoft.Extensions.Configuration;
using SportsData.Core.Config;
using SportsData.Core.DependencyInjection;
using System;

namespace SportsData.Core.Infrastructure.Refs;

/// <summary>
/// Base resource reference generator using Azure AppConfig service URLs.
/// Configured per-sport at startup. Designed to be extended by sport-specific implementations.
/// Thread-safe singleton service.
/// </summary>
public class ResourceRefGenerator : IGenerateResourceRefs
{
    private readonly string _producerBaseUrl;
    //private readonly string _contestBaseUrl;
    //private readonly string _venueBaseUrl;
    //private readonly string _franchiseBaseUrl;

    public ResourceRefGenerator(IConfiguration configuration, IAppMode appMode)
    {
        // Producer (not sport-specific at service level)
        _producerBaseUrl = configuration[CommonConfigKeys.GetProducerProviderUri()]
            ?? throw new InvalidOperationException("ProducerClientConfig:ApiUrl not configured");

        //// Contest/API (sport-specific)
        //_contestBaseUrl = configuration[CommonConfigKeys.GetContestProviderUri(appMode.CurrentSport)]
        //    ?? throw new InvalidOperationException($"ContestClientConfig:{appMode.CurrentSport}:ApiUrl not configured");

        //// Venue (not sport-specific)
        //_venueBaseUrl = configuration[CommonConfigKeys.GetVenueProviderUri()]
        //    ?? throw new InvalidOperationException("VenueClientConfig:ApiUrl not configured");

        //// Franchise (sport-specific)
        //_franchiseBaseUrl = configuration[CommonConfigKeys.GetFranchiseProviderUri(appMode.CurrentSport)]
        //    ?? throw new InvalidOperationException($"FranchiseClientConfig:{appMode.CurrentSport}:ApiUrl not configured");
    }

    // Producer resources
    public Uri ForCompetition(Guid competitionId) =>
        new Uri($"{_producerBaseUrl}/competition/{competitionId}");

    public Uri ForFranchiseSeason(Guid franchiseSeasonId) =>
        new Uri($"{_producerBaseUrl}/franchiseseason/{franchiseSeasonId}");

    public Uri ForAthlete(Guid athleteId) =>
        new Uri($"{_producerBaseUrl}/athlete/{athleteId}");

    public Uri ForAthleteSeason(Guid athleteSeasonId) =>
        new Uri($"{_producerBaseUrl}/athleteseason/{athleteSeasonId}");

    public Uri ForCoach(Guid coachId) =>
        new Uri($"{_producerBaseUrl}/coach/{coachId}");

    public Uri ForSeason(Guid seasonId) =>
        new Uri($"{_producerBaseUrl}/season/{seasonId}");

    public Uri ForSeasonPhase(Guid seasonPhaseId) =>
        new Uri($"{_producerBaseUrl}/seasonphase/{seasonPhaseId}");

    public Uri ForSeasonWeek(Guid seasonWeekId) =>
        new Uri($"{_producerBaseUrl}/seasonweek/{seasonWeekId}");

    // API/Contest resources
    public Uri ForContest(Guid contestId) =>
        new Uri($"{_producerBaseUrl}/contest/{contestId}");

    public Uri ForPick(Guid pickId) =>
        new Uri($"{_producerBaseUrl}/pick/{pickId}");

    public Uri ForRanking(int seasonYear) =>
        new Uri($"{_producerBaseUrl}/rankings/{seasonYear}");

    public Uri ForMatchupPreview(Guid contestId) =>
        new Uri($"{_producerBaseUrl}/matchuppreview/{contestId}");

    // Venue resources
    public Uri ForVenue(Guid venueId) =>
        new Uri($"{_producerBaseUrl}/venues/{venueId}");

    // Franchise resources
    public Uri ForFranchise(Guid franchiseId) =>
        new Uri($"{_producerBaseUrl}/franchise/{franchiseId}");
}
