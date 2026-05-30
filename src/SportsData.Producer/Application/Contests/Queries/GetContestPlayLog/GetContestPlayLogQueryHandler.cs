using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Contests.Queries.GetContestPlayLog;

public interface IGetContestPlayLogQueryHandler
{
    Task<Result<PlayLogDto>> ExecuteAsync(GetContestPlayLogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Returns every <see cref="CompetitionPlay"/> for a contest, ordered by
/// <c>SequenceNumber</c>. Companion to the overview handler — the overview
/// endpoint filters plays to <c>(Priority || ScoringPlay)</c> to keep its
/// payload manageable; this endpoint serves the on-demand "Show all plays"
/// expansion in the UI.
/// </summary>
public class GetContestPlayLogQueryHandler : IGetContestPlayLogQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ILogoSelectionService _logoSelectionService;
    private readonly IValidator<GetContestPlayLogQuery> _validator;
    private readonly ILogger<GetContestPlayLogQueryHandler> _logger;

    public GetContestPlayLogQueryHandler(
        TeamSportDataContext dbContext,
        ILogoSelectionService logoSelectionService,
        IValidator<GetContestPlayLogQuery> validator,
        ILogger<GetContestPlayLogQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logoSelectionService = logoSelectionService;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<PlayLogDto>> ExecuteAsync(
        GetContestPlayLogQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return new Failure<PlayLogDto>(
                default!,
                ResultStatus.Validation,
                validationResult.Errors);
        }

        var contest = await _dbContext.Contests
            .AsNoTracking()
            .Include(x => x.AwayTeamFranchiseSeason!)
            .ThenInclude(x => x.Franchise)
            .ThenInclude(x => x.Logos)
            .Include(x => x.HomeTeamFranchiseSeason!)
            .ThenInclude(x => x.Franchise)
            .ThenInclude(x => x.Logos)
            .Include(x => x.AwayTeamFranchiseSeason!)
            .ThenInclude(x => x.Logos!)
            .Include(x => x.HomeTeamFranchiseSeason!)
            .ThenInclude(x => x.Logos!)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == query.ContestId, cancellationToken);

        if (contest is null)
        {
            return new Failure<PlayLogDto>(
                default!,
                ResultStatus.NotFound,
                [new FluentValidation.Results.ValidationFailure(
                    nameof(query.ContestId),
                    $"Contest with ID {query.ContestId} not found")]);
        }

        var awayTeamSlug = contest.AwayTeamFranchiseSeason!.Franchise!.Slug!;
        var homeTeamSlug = contest.HomeTeamFranchiseSeason!.Franchise!.Slug!;

        var awayTeamLogoUri = _logoSelectionService.SelectLogoForDarkBackground(contest.AwayTeamFranchiseSeason?.Logos)
                              ?? _logoSelectionService.SelectLogoForDarkBackground(contest.AwayTeamFranchiseSeason?.Franchise?.Logos);
        var homeTeamLogoUri = _logoSelectionService.SelectLogoForDarkBackground(contest.HomeTeamFranchiseSeason?.Logos)
                              ?? _logoSelectionService.SelectLogoForDarkBackground(contest.HomeTeamFranchiseSeason?.Franchise?.Logos);

        var awayTeamFranchiseSeasonId = contest.AwayTeamFranchiseSeasonId;

        // Full set — no Priority/ScoringPlay filter. For an MLB game this can
        // be ~500 rows; for football typically ~150. Caller has opted into
        // the larger payload via the "Show all plays" UI toggle.
        var plays = await _dbContext.CompetitionPlays
            .AsNoTracking()
            .Include(p => p.Competition)
            .Where(p => p.Competition.ContestId == query.ContestId)
            .OrderBy(p => p.SequenceNumber)
            .ToListAsync(cancellationToken);

        var homeTeamFranchiseSeasonId = contest.HomeTeamFranchiseSeasonId;

        var playDtos = plays.Select((p, x) =>
        {
            // Mirrors the mapping in GetContestOverviewQueryHandler.GetPlayLogAsync.
            // StartFranchiseSeasonId is the start team (football) / batting team
            // (baseball) — both serve as "team on play" for display. Neutral
            // plays (no attribution, or attribution to a third party we don't
            // recognize) get Team = null so the UI renders no logo rather than
            // mis-attributing to home.
            var teamId = p.StartFranchiseSeasonId ?? Guid.Empty;
            string? teamSlug = null;
            if (teamId == awayTeamFranchiseSeasonId) teamSlug = awayTeamSlug;
            else if (teamId == homeTeamFranchiseSeasonId) teamSlug = homeTeamSlug;

            return new PlayDto
            {
                Ordinal = x,
                Quarter = p.PeriodNumber,
                FranchiseSeasonId = teamId,
                Team = teamSlug,
                Description = p.ShortAlternativeText ?? p.Text,
                TimeRemaining = (p as FootballCompetitionPlay)?.ClockDisplayValue,
                IsScoringPlay = p.ScoringPlay,
                IsKeyPlay = p.Priority
            };
        }).ToList();

        var dto = new PlayLogDto
        {
            AwayTeamSlug = awayTeamSlug,
            HomeTeamSlug = homeTeamSlug,
            AwayTeamLogoUrl = awayTeamLogoUri is null ? string.Empty : awayTeamLogoUri.OriginalString,
            HomeTeamLogoUrl = homeTeamLogoUri is null ? string.Empty : homeTeamLogoUri.OriginalString,
            Plays = playDtos
        };

        return new Success<PlayLogDto>(dto);
    }
}
