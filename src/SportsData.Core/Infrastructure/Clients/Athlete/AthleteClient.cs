using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Athlete;

public interface IProvideAthletes : IProvideHealthChecks
{
    /// <summary>Full athlete drill-down (athlete record + every season + statistics). Keyed by GUID — athlete slugs are not unique (~15% collide).</summary>
    Task<Result<AthleteDetailDto>> GetAthleteDetails(Guid athleteId, CancellationToken cancellationToken = default);

    /// <summary>Athletes at a position with their week opponent, the opponent's defensive allowance per game, and current/previous season stat blocks.</summary>
    Task<Result<AthleteMatchupSummariesDto>> GetAthleteMatchupSummaries(string position, int seasonYear, int week, int seasonPhaseTypeCode = 2, CancellationToken cancellationToken = default);

    /// <summary>Batch box-score statlines (category.statName → value) per (athleteSeason, contest) — one call per lineup for scoring.</summary>
    Task<Result<List<AthleteStatlineDto>>> GetAthleteStatlines(List<Guid> contestIds, List<Guid> athleteSeasonIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Athlete-domain reads. These are served by the same Producer instance
/// the franchise reads target (Producer's AthleteController) — the
/// factory reuses the franchise ApiUrl configuration — but the CLIENT is
/// its own aggregate root: athlete reads do not belong on the franchise
/// domain, and the dormant Player service's client stays untouched.
/// </summary>
public class AthleteClient : ClientBase, IProvideAthletes
{
    private readonly ILogger<AthleteClient> _logger;

    public AthleteClient(
        ILogger<AthleteClient> logger,
        HttpClient httpClient) :
        base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<AthleteDetailDto>> GetAthleteDetails(Guid athleteId, CancellationToken cancellationToken = default)
    {
        return await GetAsync(
            $"athletes/{athleteId}",
            new AthleteDetailDto(),
            entityName: "AthleteDetail",
            cancellationToken: cancellationToken);
    }

    public async Task<Result<AthleteMatchupSummariesDto>> GetAthleteMatchupSummaries(
        string position,
        int seasonYear,
        int week,
        int seasonPhaseTypeCode = 2,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync(
            $"athletes/matchup-summaries?position={Uri.EscapeDataString(position)}&seasonYear={seasonYear}&week={week}&phaseTypeCode={seasonPhaseTypeCode}",
            new AthleteMatchupSummariesDto(),
            entityName: "AthleteMatchupSummaries",
            cancellationToken: cancellationToken);
    }

    public async Task<Result<List<AthleteStatlineDto>>> GetAthleteStatlines(
        List<Guid> contestIds,
        List<Guid> athleteSeasonIds,
        CancellationToken cancellationToken = default)
    {
        if (contestIds is null || contestIds.Count == 0 ||
            athleteSeasonIds is null || athleteSeasonIds.Count == 0)
        {
            return new Success<List<AthleteStatlineDto>>([]);
        }

        var request = new GetAthleteStatlinesRequest(contestIds.ToArray(), athleteSeasonIds.ToArray());
        var result = await PostOrDefaultAsync<List<AthleteStatlineDto>, GetAthleteStatlinesRequest>(
            "athletes/statlines", request, [], cancellationToken);
        return new Success<List<AthleteStatlineDto>>(result);
    }
}
