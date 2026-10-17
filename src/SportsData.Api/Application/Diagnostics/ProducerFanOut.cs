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
    /// the default Producer more than once. The same fallback can misroute a
    /// listed sport whose URI is missing; <see cref="CollectAsync{T}"/> catches
    /// that from the rows' own Sport.
    /// </summary>
    public static readonly IReadOnlyList<Sport> Sports = [Sport.FootballNcaa, Sport.FootballNfl];

    /// <summary>
    /// Calls <paramref name="query"/> for every sport in parallel and
    /// concatenates the rows in sport order. All or nothing: if any Producer
    /// fails, is unreachable, or answers with another sport's rows (a missing
    /// sport-specific URI falls back to the default Producer), the result is
    /// a failure naming each failed sport. A diagnostic that silently drops
    /// or double-counts a sport would misreport it.
    /// </summary>
    /// <param name="sportOf">The sport a row belongs to (each Producer stamps its rows).</param>
    public static async Task<Result<List<T>>> CollectAsync<T>(
        IReadOnlyList<Sport> sports,
        Func<Sport, CancellationToken, Task<Result<List<T>>>> query,
        Func<T, Sport> sportOf,
        ILogger logger,
        string what,
        CancellationToken cancellationToken)
    {
        var answers = await Task.WhenAll(sports.Select(async sport =>
        {
            try
            {
                var result = await query(sport, cancellationToken);

                var foreign = result.IsSuccess
                    ? (result.Value ?? []).Select(sportOf).Where(s => s != sport).Distinct().ToList()
                    : [];
                if (foreign.Count > 0)
                {
                    logger.LogError(
                        "Producer slot {Sport} answered {What} with rows for {ForeignSports}; check its sport-specific Producer URI",
                        sport, what, foreign);
                    return (Sport: sport, Result: (Result<List<T>>)new Failure<List<T>>(
                        [],
                        ResultStatus.Error,
                        [new ValidationFailure(sport.ToString(),
                            $"The {sport} Producer call returned {string.Join(", ", foreign)} rows; its Producer URI is likely missing or wrong.")]));
                }

                return (Sport: sport, Result: result);
            }
            // Transport failures (and anything else, such as a client with no
            // base address) throw rather than return a Failure; name the
            // sport. Caller cancellation still propagates.
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Producer {Sport} call failed while fetching {What}", sport, what);
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
