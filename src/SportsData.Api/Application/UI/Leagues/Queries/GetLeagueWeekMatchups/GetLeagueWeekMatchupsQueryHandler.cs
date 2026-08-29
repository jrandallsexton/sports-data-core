using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Mapping;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

public interface IGetLeagueWeekMatchupsQueryHandler
{
    Task<Result<LeagueWeekMatchupsDto>> ExecuteAsync(
        GetLeagueWeekMatchupsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueWeekMatchupsQueryHandler : IGetLeagueWeekMatchupsQueryHandler
{
    private readonly ILogger<GetLeagueWeekMatchupsQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILeagueMembershipGuard _membershipGuard;

    public GetLeagueWeekMatchupsQueryHandler(
        ILogger<GetLeagueWeekMatchupsQueryHandler> logger,
        AppDataContext dbContext,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider,
        ILeagueMembershipGuard membershipGuard)
    {
        _logger = logger;
        _dbContext = dbContext;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
        _membershipGuard = membershipGuard;
    }

    public async Task<Result<LeagueWeekMatchupsDto>> ExecuteAsync(
        GetLeagueWeekMatchupsQuery query,
        CancellationToken cancellationToken = default)
    {
        // The league's slate + spreads — members only.
        // See docs/audit/league-authorization-idor.md.
        if (!await _membershipGuard.IsMemberAsync(query.LeagueId, query.UserId, cancellationToken))
        {
            return new Failure<LeagueWeekMatchupsDto>(
                default!,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(query.LeagueId), "You are not a member of this league.")]);
        }

        _logger.LogInformation(
            "GetLeagueWeekMatchupsQueryHandler.ExecuteAsync called with userId={UserId}, leagueId={LeagueId}, week={Week}",
            query.UserId,
            query.LeagueId,
            query.Week);

        try
        {
            _logger.LogDebug(
                "Querying database for league, leagueId={LeagueId}",
                query.LeagueId);

            var league = await _dbContext.PickemGroups
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken: cancellationToken);

            if (league is null)
            {
                _logger.LogWarning(
                    "League not found, leagueId={LeagueId}, userId={UserId}, week={Week}",
                    query.LeagueId,
                    query.UserId,
                    query.Week);

                return new Failure<LeagueWeekMatchupsDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(query.LeagueId), "League not found")]);
            }

            _logger.LogInformation(
                "League found: {LeagueName}, PickType={PickType}, leagueId={LeagueId}",
                league.Name,
                league.PickType,
                query.LeagueId);

            _logger.LogDebug(
                "Querying database for league matchups, leagueId={LeagueId}, week={Week}",
                query.LeagueId,
                query.Week);

            var groupMatchups = await _dbContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(x => x.GroupId == query.LeagueId && x.SeasonWeek == query.Week)
                .Select(x => new
                {
                    x.StartDateUtc,
                    x.ContestId,
                    x.AwayRank,
                    x.HomeRank,
                    x.Headline,
                    x.SeasonYear
                })
                .ToListAsync(cancellationToken);

            var matchups = groupMatchups
                .Select(x => new LeagueWeekMatchupsDto.MatchupForPickDto
                {
                    StartDateUtc = x.StartDateUtc,
                    ContestId = x.ContestId,
                    AwayRank = x.AwayRank,
                    HomeRank = x.HomeRank,
                    HeadLine = x.Headline
                })
                .ToList();

            // Season year is authoritative on PickemGroupMatchup (set at generation
            // time). Falls back to the current UTC year (via IDateTimeProvider for
            // deterministic testing) only when a week returned zero matchups — which
            // can't cleanly infer a year from the data itself.
            var seasonYear = groupMatchups.FirstOrDefault()?.SeasonYear ?? _dateTimeProvider.UtcNow().Year;

            _logger.LogInformation(
                "Retrieved {Count} matchups from database for leagueId={LeagueId}, week={Week}",
                matchups.Count,
                query.LeagueId,
                query.Week);

            var contestIds = matchups.Select(x => x.ContestId).Distinct().ToList();

            _logger.LogDebug(
                "Calling ContestClient.GetMatchupsByContestIds for {ContestCount} contests, leagueId={LeagueId}, week={Week}",
                contestIds.Count,
                query.LeagueId,
                query.Week);

            // TODO: read direction from user.PreferredMark once the profile-
            // toggle UI ships. For now every user sees roundels. See
            // docs/team-mark-user-preference-design.md.
            var direction = MarkDirection.Roundel;

            // The Producer round trip is this handler's long pole — start
            // it FIRST and run the local predictions/previews queries while
            // it's in flight. Those two stay sequential relative to each
            // other (one DbContext can't run concurrent operations), but
            // they overlap the HTTP call, so total ≈ max(http, local)
            // instead of the sum.
            // Timed INSIDE the async wrapper so the measurement ends when
            // Producer responds — not when this handler gets around to
            // awaiting. Stopping a timer after the await would report
            // max(producer, local) and blame Producer for slow local
            // queries whenever the overlap goes the other way.
            long producerLegMs = -1;
            async Task<Result<List<SportsData.Core.Dtos.Canonical.LeagueMatchupDto>>> CallProducerTimedAsync()
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    return await _contestClientFactory
                        .Resolve(league.Sport)
                        .GetMatchupsByContestIds(contestIds, direction, cancellationToken);
                }
                finally
                {
                    producerLegMs = sw.ElapsedMilliseconds;
                }
            }

            var matchupsTask = CallProducerTimedAsync();

            List<ContestPredictionDto> predictions;
            List<MatchupPreviewProjection> previews;
            try
            {
                // Straight into the wire DTO — the handler consumes exactly
                // these five fields, and projecting here removes the manual
                // per-matchup mapping loop below.
                predictions = await _dbContext.ContestPredictions
                    .Where(x => contestIds.Contains(x.ContestId))
                    .AsNoTracking()
                    .Select(x => new ContestPredictionDto
                    {
                        ContestId = x.ContestId,
                        ModelVersion = x.ModelVersion,
                        PredictionType = x.PredictionType,
                        WinProbability = x.WinProbability,
                        WinnerFranchiseSeasonId = x.WinnerFranchiseSeasonId,
                    })
                    .ToListAsync(cancellationToken);

                // Projection, not entities: MatchupPreview rows carry the full
                // AI-generated preview text; this handler reads six scalars.
                previews = await _dbContext.MatchupPreviews
                    .Where(x => contestIds.Contains(x.ContestId) && x.RejectedUtc == null)
                    .AsNoTracking()
                    .Select(x => new MatchupPreviewProjection(
                        x.ContestId,
                        x.CreatedUtc,
                        x.ApprovedUtc,
                        x.RejectedUtc,
                        x.PredictedStraightUpWinner,
                        x.PredictedSpreadWinner))
                    .ToListAsync(cancellationToken);
            }
            catch
            {
                // The Producer call is still in flight; observe its eventual
                // fault so it can't surface as an unobserved task exception.
                // (Its own response is simply discarded — the request token
                // still cancels it if the caller aborted.)
                _ = matchupsTask.ContinueWith(
                    static t => _ = t.Exception,
                    TaskContinuationOptions.OnlyOnFaulted);
                throw;
            }

            _logger.LogDebug(
                "Found {PredictionCount} contest predictions and {PreviewCount} matchup previews, leagueId={LeagueId}, week={Week}",
                predictions.Count,
                previews.Count,
                query.LeagueId,
                query.Week);

            var matchupsResult = await matchupsTask;
            if (!matchupsResult.IsSuccess)
            {
                _logger.LogError("Failed to retrieve canonical matchups for leagueId={LeagueId}, week={Week}", query.LeagueId, query.Week);
                return new Failure<LeagueWeekMatchupsDto>(
                    default!,
                    ResultStatus.Error,
                    [new FluentValidation.Results.ValidationFailure("matchups", "Failed to retrieve matchup data from Producer")]);
            }
            var canonicalMatchups = matchupsResult.Value;

            _logger.LogInformation(
                "Received {CanonicalCount} canonical matchups from ContestClient for leagueId={LeagueId}, week={Week}",
                canonicalMatchups?.Count ?? 0,
                query.LeagueId,
                query.Week);

            if (canonicalMatchups == null || canonicalMatchups.Count == 0)
            {
                _logger.LogWarning(
                    "No canonical matchups returned from ContestClient for leagueId={LeagueId}, week={Week}",
                    query.LeagueId,
                    query.Week);
                canonicalMatchups = [];
            }

            // Create dictionary for fast lookup of canonical values
            var canonicalMap = canonicalMatchups.ToDictionary(x => x.ContestId);

            _logger.LogDebug(
                "Enriching {MatchupCount} matchups with canonical data, leagueId={LeagueId}, week={Week}",
                matchups.Count,
                query.LeagueId,
                query.Week);

            // Fill in canonical fields for each league matchup
            foreach (var matchup in matchups)
            {
                if (canonicalMap.TryGetValue(matchup.ContestId, out var canonical))
                {
                    // Canonical fields (teams, odds, scores, status, probables,
                    // streaming, etc.) — extracted to MatchupForPickDtoMapper so
                    // the admin debug endpoint can reuse the same shape without
                    // a league context. League-context fields (HeadLine,
                    // Predictions, AiWinner, IsPreview*) stay below.
                    MatchupForPickDtoMapper.ApplyCanonical(matchup, canonical, league.Sport);

                    // Headline priority: live CompetitionNote.Headline (marquee
                    // tag — bowl/conf championship/postseason designation) wins,
                    // baseball CurrentSeriesSummary is the regular-season fallback
                    // (e.g. "BOS leads series 2-0"), frozen PickemGroupMatchup
                    // value (already on matchup.HeadLine from the initial
                    // projection) is the last-resort safety net for historical
                    // leagues whose CompetitionNote may no longer resolve.
                    // Whitespace guard: cn."Headline" is non-null in the schema
                    // but could be empty/blank; treat empty as missing so the
                    // fallback chain isn't suppressed.
                    matchup.HeadLine = !string.IsNullOrWhiteSpace(canonical.Headline)
                        ? canonical.Headline
                        : !string.IsNullOrWhiteSpace(canonical.CurrentSeriesSummary)
                            ? canonical.CurrentSeriesSummary
                            : matchup.HeadLine;

                    var preview = previews
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.RejectedUtc == null)
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefault();

                    if (preview != null)
                    {
                        if (league.PickType == PickType.StraightUp)
                        {
                            matchup.AiWinnerFranchiseSeasonId = preview.PredictedStraightUpWinner;
                        }
                        else
                        {
                            matchup.AiWinnerFranchiseSeasonId = preview.PredictedSpreadWinner ?? preview.PredictedStraightUpWinner;
                        }
                    }

                    matchup.IsPreviewAvailable = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                   x.RejectedUtc == null);

                    matchup.IsPreviewReviewed = previews.Any(x => x.ContestId == matchup.ContestId &&
                                                                  x is { ApprovedUtc: not null, RejectedUtc: null });

                    matchup.Predictions.AddRange(
                        predictions.Where(x => x.ContestId == matchup.ContestId));
                }
                else
                {
                    _logger.LogWarning(
                        "No canonical matchup found for ContestId={ContestId}, leagueId={LeagueId}, week={Week}",
                        matchup.ContestId,
                        query.LeagueId,
                        query.Week);
                }
            }

            _logger.LogDebug(
                "Finished enriching matchups, creating result DTO for leagueId={LeagueId}, week={Week}",
                query.LeagueId,
                query.Week);

            // All canonical matchups in a single league-week share the same
            // SeasonWeek.EndDate (they're picked from the same SeasonWeek
            // bucket), so any row is authoritative. Null when no canonical
            // matchups came back (empty week / missing Producer data).
            var asOfDate = canonicalMatchups.FirstOrDefault()?.SeasonWeekEndDate;

            var result = new LeagueWeekMatchupsDto
            {
                PickType = league!.PickType,
                UseConfidencePoints = league!.UseConfidencePoints,
                SeasonYear = seasonYear,
                WeekNumber = query.Week,
                AsOfDate = asOfDate,
                Sport = league.Sport.ToString(),
                Matchups = matchups.OrderBy(x => x.StartDateUtc).ToList()
            };

            // ProducerLegMs = dispatch → response for the overlapped canonical
            // call, the historical long pole of this endpoint. Pairs with
            // Producer's "Canonical matchups served" event under the same
            // @TraceId (surfaced to clients as the X-Trace-Id header).
            _logger.LogInformation(
                "Successfully completed GetLeagueWeekMatchupsQueryHandler.ExecuteAsync for leagueId={LeagueId}, week={Week}, userId={UserId}, returning {Count} matchups. ProducerLegMs={ProducerLegMs}",
                query.LeagueId,
                query.Week,
                query.UserId,
                result.Matchups.Count,
                producerLegMs);

            return new Success<LeagueWeekMatchupsDto>(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error in GetLeagueWeekMatchupsQueryHandler.ExecuteAsync for leagueId={LeagueId}, week={Week}, userId={UserId}",
                query.LeagueId,
                query.Week,
                query.UserId);

            return new Failure<LeagueWeekMatchupsDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(query.LeagueId), $"Error retrieving matchups: {ex.Message}")]);
        }
    }
}

/// <summary>
/// The six scalars this handler reads from MatchupPreview — a named
/// projection so the query contract is explicit and the AI preview text
/// never leaves the database.
/// </summary>
internal sealed record MatchupPreviewProjection(
    Guid ContestId,
    DateTime CreatedUtc,
    DateTime? ApprovedUtc,
    DateTime? RejectedUtc,
    Guid? PredictedStraightUpWinner,
    Guid? PredictedSpreadWinner);
