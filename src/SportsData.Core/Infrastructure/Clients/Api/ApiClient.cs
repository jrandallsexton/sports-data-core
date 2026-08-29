using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Api;

/// <summary>
/// Wire shape for POST system/league-contests/in-use on the API service.
/// </summary>
public record GetContestIdsInLeaguesRequest(Guid[] ContestIds);

/// <summary>
/// Producer → API inquiries. Today that is one question: which of these
/// contests appear in any pick'em league's matchups? The
/// CompetitionStreamScheduler uses the answer to live-source ONLY games
/// that back a league instead of every game ESPN publishes (2026-08-29:
/// 688 of 729 scheduled NCAA streams served no league). Callers must
/// FAIL OPEN on Failure — blinding league games on game day is worse
/// than briefly over-sourcing.
/// </summary>
public interface IProvideApi : Middleware.Health.IProvideHealthChecks
{
    Task<Result<List<Guid>>> GetContestIdsInLeagues(List<Guid> contestIds, CancellationToken ct = default);
}

public class ApiClient : ClientBase, IProvideApi
{
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(ILogger<ApiClient> logger, HttpClient httpClient)
        : base(httpClient)
    {
        _logger = logger;
    }

    public async Task<Result<List<Guid>>> GetContestIdsInLeagues(List<Guid> contestIds, CancellationToken ct = default)
    {
        if (contestIds is null || contestIds.Count == 0)
            return new Success<List<Guid>>([]);

        try
        {
            // Null payload must surface as Failure, never as Success([]) —
            // an empty SUCCESS means "none of these back a league" and
            // would make the scheduler cull every league stream, the exact
            // outcome the fail-open contract exists to prevent.
            var result = await PostOrDefaultAsync<List<Guid>, GetContestIdsInLeaguesRequest>(
                "system/league-contests/in-use",
                new GetContestIdsInLeaguesRequest(contestIds.ToArray()),
                null!,
                ct);

            if (result is null)
            {
                _logger.LogWarning("GetContestIdsInLeagues returned a null payload; caller should fail open.");
                return new Failure<List<Guid>>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("response", "Null payload from system/league-contests/in-use.")]);
            }

            return new Success<List<Guid>>(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetContestIdsInLeagues failed; caller should fail open.");
            return new Failure<List<Guid>>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("contestIds", ex.Message)]);
        }
    }
}

/// <summary>
/// Fallback registration for services whose configuration carries no
/// <c>CommonConfig:ApiClientConfig:ApiUrl</c> — every call returns
/// Failure so callers take their fail-open path. Keeps IProvideApi
/// resolvable everywhere without forcing an AppConfig backfill on
/// services that never call the API.
/// </summary>
public class UnconfiguredApiClient : IProvideApi
{
    public Task<Result<List<Guid>>> GetContestIdsInLeagues(List<Guid> contestIds, CancellationToken ct = default) =>
        Task.FromResult<Result<List<Guid>>>(new Failure<List<Guid>>(
            default!,
            ResultStatus.Error,
            [new ValidationFailure("ApiClientConfig", "ApiClientConfig:ApiUrl is not configured for this service.")]));

    public string GetProviderName() => nameof(UnconfiguredApiClient);

    public Task<Dictionary<string, object>> GetHealthStatus() =>
        Task.FromResult(new Dictionary<string, object> { ["configured"] = false });
}
