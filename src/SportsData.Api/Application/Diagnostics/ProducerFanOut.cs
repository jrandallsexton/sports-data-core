using FluentValidation.Results;

using SportsData.Core.Common;

namespace SportsData.Api.Application.Diagnostics;

/// <summary>
/// Diagnostics read each sport's Producer over HTTP and merge the answers;
/// API never queries a Producer database. This replaces
/// CanonicalAdminDataProvider, which ran embedded SQL against ONE canonical
/// connection and so only ever saw one sport.
/// </summary>
internal static class ProducerFanOut
{
    /// <summary>
    /// The Producers the diagnostics read. Explicit on purpose:
    /// ContestClientFactory falls back to the default Producer URI for a
    /// sport with none configured, so iterating every Sport value would query
    /// the default Producer more than once and double-count its rows.
    /// </summary>
    public static readonly IReadOnlyList<Sport> Sports = [Sport.FootballNcaa, Sport.FootballNfl];

    /// <summary>
    /// Calls <paramref name="query"/> for every sport in parallel and
    /// concatenates the rows in sport order. All or nothing: if any Producer
    /// fails or is unreachable, the result is a failure naming each failed
    /// sport. A diagnostic that silently drops a sport would report that
    /// sport as clean.
    /// </summary>
    public static async Task<Result<List<T>>> CollectAsync<T>(
        IReadOnlyList<Sport> sports,
        Func<Sport, CancellationToken, Task<Result<List<T>>>> query,
        ILogger logger,
        string what,
        CancellationToken cancellationToken)
    {
        var answers = await Task.WhenAll(sports.Select(async sport =>
        {
            try
            {
                return (Sport: sport, Result: await query(sport, cancellationToken));
            }
            // Transport failures throw from HttpClient rather than returning
            // a Failure. Caller cancellation still propagates.
            catch (Exception ex) when (
                ex is HttpRequestException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
            {
                logger.LogError(ex, "Producer {Sport} unreachable while fetching {What}", sport, what);
                return (Sport: sport, Result: (Result<List<T>>)new Failure<List<T>>(
                    [],
                    ResultStatus.Error,
                    [new ValidationFailure(sport.ToString(), $"{ex.GetType().Name} calling Producer {sport}")]));
            }
        }));

        var failed = answers.Where(a => !a.Result.IsSuccess).ToList();
        if (failed.Count > 0)
        {
            foreach (var (sport, result) in failed)
            {
                logger.LogError(
                    "Producer {Sport} failed {What} with {Status}: {Errors}",
                    sport, what, result.Status,
                    string.Join("; ", ((Failure<List<T>>)result).Errors.Select(e => e.ErrorMessage)));
            }

            return new Failure<List<T>>(
                [],
                ResultStatus.Error,
                failed.Select(a => new ValidationFailure(
                    a.Sport.ToString(),
                    $"Producer {a.Sport} failed to return {what}; the result would be incomplete.")).ToList());
        }

        return new Success<List<T>>(answers.SelectMany(a => a.Result.Value ?? []).ToList());
    }
}
