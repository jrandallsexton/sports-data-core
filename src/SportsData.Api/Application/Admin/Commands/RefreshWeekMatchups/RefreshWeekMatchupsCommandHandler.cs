using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.Commands.RefreshWeekMatchups;

public interface IRefreshWeekMatchupsCommandHandler
{
    Task<Result<RefreshWeekMatchupsResponse>> ExecuteAsync(
        RefreshWeekMatchupsCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves a season week, then enqueues a refresh pass per active league
/// holding that week.
/// </summary>
/// <remarks>
/// The record columns on PickemGroupMatchup are a COPY taken when the week is
/// generated, not a live read — the league-week query projects the stored
/// column and never derives. Anything that corrects a FranchiseSeason after
/// generation therefore leaves the cards frozen at generation-time values, and
/// no amount of cache eviction helps because the stale numbers are committed
/// rows.
/// <para>
/// Until this existed, the only caller passing IsRefresh was the poll-release
/// handler, which fires BEFORE the games are played. That left no way to
/// re-sync a week once results settled.
/// </para>
/// <para>
/// Safe to re-run: the processor upserts by ContestId, so existing matchups
/// take attribute updates, newly-eligible contests are inserted, and contests
/// that fell out of the filter are left alone so picks against them survive.
/// Each enqueued job evicts its own league-week cache on completion.
/// </para>
/// </remarks>
public class RefreshWeekMatchupsCommandHandler : IRefreshWeekMatchupsCommandHandler
{
    private readonly AppDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IValidator<RefreshWeekMatchupsCommand> _validator;
    private readonly ILogger<RefreshWeekMatchupsCommandHandler> _logger;

    public RefreshWeekMatchupsCommandHandler(
        AppDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IValidator<RefreshWeekMatchupsCommand> validator,
        ILogger<RefreshWeekMatchupsCommandHandler> logger)
    {
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<RefreshWeekMatchupsResponse>> ExecuteAsync(
        RefreshWeekMatchupsCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<RefreshWeekMatchupsResponse>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        var seasonWeekId = command.SeasonWeekId;

        if (seasonWeekId is null)
        {
            var (resolved, failure) = await ResolveSeasonWeekIdAsync(command, cancellationToken);
            if (failure is not null)
                return failure;

            seasonWeekId = resolved;
        }

        // Every active league holding a week shell for this SeasonWeekId.
        // Deliberately NOT narrowed to ranked leagues the way the poll-release
        // path is: conference-only leagues carry the same record snapshots and
        // go just as stale.
        var affected = await _dataContext.PickemGroupWeeks
            .AsNoTracking()
            .Where(w => w.SeasonWeekId == seasonWeekId!.Value && w.Group.DeactivatedUtc == null)
            .Select(w => new { w.GroupId, w.SeasonYear, w.SeasonWeek, w.IsNonStandardWeek })
            .ToListAsync(cancellationToken);

        if (affected.Count == 0)
        {
            return new Failure<RefreshWeekMatchupsResponse>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(
                    nameof(command.SeasonWeekId),
                    $"No active leagues have a week for SeasonWeekId {seasonWeekId}.")]);
        }

        var correlationId = Guid.NewGuid();

        foreach (var league in affected)
        {
            _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(
                new ScheduleGroupWeekMatchupsCommand(
                    league.GroupId,
                    seasonWeekId!.Value,
                    league.SeasonYear,
                    league.SeasonWeek,
                    league.IsNonStandardWeek,
                    correlationId,
                    IsRefresh: true)));
        }

        _logger.LogInformation(
            "Admin matchup refresh enqueued for {Count} league(s). SeasonWeekId={SeasonWeekId}, CorrelationId={CorrelationId}",
            affected.Count, seasonWeekId, correlationId);

        return new Success<RefreshWeekMatchupsResponse>(new RefreshWeekMatchupsResponse
        {
            SeasonWeekId = seasonWeekId!.Value,
            LeaguesQueued = affected.Count,
            CorrelationId = correlationId
        });
    }

    /// <summary>
    /// Resolves the year/week convenience form against the league weeks that
    /// actually exist. Ambiguity is REPORTED, never guessed — picking one and
    /// silently refreshing the wrong slate is the worse failure.
    /// </summary>
    private async Task<(Guid? SeasonWeekId, Failure<RefreshWeekMatchupsResponse>? Failure)> ResolveSeasonWeekIdAsync(
        RefreshWeekMatchupsCommand command,
        CancellationToken cancellationToken)
    {
        // Small by construction — one row per league holding this week number —
        // so the grouping is done in memory rather than contorted into a
        // translatable GroupBy.
        var weeks = await _dataContext.PickemGroupWeeks
            .AsNoTracking()
            .Where(w => w.SeasonYear == command.SeasonYear!.Value
                        && w.SeasonWeek == command.SeasonWeek!.Value
                        && w.Group.DeactivatedUtc == null
                        && (command.Sport == null || w.Group.Sport == command.Sport.Value))
            .Select(w => new { w.SeasonWeekId, w.Group.Sport, w.SeasonPhaseTypeCode, w.GroupId })
            .ToListAsync(cancellationToken);

        if (weeks.Count == 0)
        {
            return (null, new Failure<RefreshWeekMatchupsResponse>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(
                    nameof(command.SeasonWeek),
                    $"No active league weeks found for {command.SeasonYear} week {command.SeasonWeek}"
                    + (command.Sport is null ? "." : $" in {command.Sport}."))]));
        }

        // A SeasonWeekId belongs to exactly one sport and phase, so this groups
        // rather than splits.
        var candidates = weeks
            .GroupBy(w => w.SeasonWeekId)
            .Select(grp => new
            {
                SeasonWeekId = grp.Key,
                Sport = grp.First().Sport.ToString(),
                SeasonPhaseTypeCode = grp.First().SeasonPhaseTypeCode,
                Leagues = grp.Select(x => x.GroupId).Distinct().Count()
            })
            .OrderBy(x => x.Sport)
            .ThenBy(x => x.SeasonPhaseTypeCode)
            .ToList();

        if (candidates.Count > 1)
        {
            // The candidates have to say what they ARE. Returning bare guids
            // left the caller to query the database to tell them apart, and on
            // 2026-09-18 that produced a blind pick between NCAA regular
            // season, NFL preseason and NFL regular season.
            var detail = string.Join("; ", candidates.Select(c =>
                $"{c.SeasonWeekId} = {c.Sport} phase {c.SeasonPhaseTypeCode} ({c.Leagues} league(s))"));

            return (null, new Failure<RefreshWeekMatchupsResponse>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(
                    nameof(command.SeasonWeek),
                    $"{command.SeasonYear} week {command.SeasonWeek} is ambiguous; narrow it with sport, "
                    + "or re-call with an explicit seasonWeekId. Phase codes: 1 preseason, 2 regular season, "
                    + $"3 postseason. Candidates: {detail}")]));
        }

        return (candidates[0].SeasonWeekId, null);
    }
}

public class RefreshWeekMatchupsResponse
{
    public Guid SeasonWeekId { get; init; }

    public int LeaguesQueued { get; init; }

    public Guid CorrelationId { get; init; }
}
