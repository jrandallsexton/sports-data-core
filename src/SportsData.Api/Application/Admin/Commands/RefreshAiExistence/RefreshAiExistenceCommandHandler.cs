using FluentValidation.Results;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.Commands.RefreshAiExistence;

public interface IRefreshAiExistenceCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(RefreshAiExistenceCommand command, CancellationToken cancellationToken = default);
}

public class RefreshAiExistenceCommandHandler : IRefreshAiExistenceCommandHandler
{
    private readonly ISeasonClientFactory _seasonClientFactory;
    private readonly AppDataContext _dataContext;
    private readonly ILogger<RefreshAiExistenceCommandHandler> _logger;
    private readonly ISyntheticPickService _syntheticPickService;
    private readonly IStatBotPickWriter _statBotPickWriter;

    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IValidator<RefreshAiExistenceCommand> _validator;

    public RefreshAiExistenceCommandHandler(
        ILogger<RefreshAiExistenceCommandHandler> logger,
        AppDataContext dataContext,
        ISeasonClientFactory seasonClientFactory,
        ISyntheticPickService syntheticPickService,
        IStatBotPickWriter statBotPickWriter,
        IProvideBackgroundJobs backgroundJobProvider,
        IValidator<RefreshAiExistenceCommand> validator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _seasonClientFactory = seasonClientFactory;
        _syntheticPickService = syntheticPickService;
        _statBotPickWriter = statBotPickWriter;
        _backgroundJobProvider = backgroundJobProvider;
        _validator = validator;
    }

    public async Task<Result<Guid>> ExecuteAsync(RefreshAiExistenceCommand command, CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<Guid>(Guid.Empty, ResultStatus.Validation, validation.Errors);
        }

        try
        {
            // The current week anchors the season; command.Week can name any
            // week of that season so a missed week is backfillable.
            // TODO: multi-sport — resolve sport from context instead of defaulting
            var weekResult = await _seasonClientFactory.Resolve(Sport.FootballNcaa).GetCurrentSeasonWeek(cancellationToken);
            var currentWeek = weekResult.IsSuccess ? weekResult.Value : null;

            if (currentWeek is null)
            {
                _logger.LogError("Current week could not be found");
                return new Failure<Guid>(
                    default,
                    ResultStatus.NotFound,
                    new List<ValidationFailure>
                    {
                        new ValidationFailure("CurrentWeek", "Current week could not be found")
                    });
            }

            var seasonYear = currentWeek.SeasonYear;
            var week = command.Week ?? currentWeek.WeekNumber;

            // get the synthetics
            var synthetics = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic)
                .ToListAsync(cancellationToken);

            // get all pickemGroups
            var allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync(cancellationToken);

            var totalAddedToGroupCount = 0;

            foreach (var synthetic in synthetics)
            {
                // we need to make sure a synthetic exists in each league
                foreach (var group in allGroups)
                {
                    var groupSynthetic = group.Members
                        .FirstOrDefault(m => m.UserId == synthetic.Id);

                    if (groupSynthetic is not null)
                        continue;

                    // add the synthetic to the group
                    await _dataContext.PickemGroupMembers.AddAsync(
                        new PickemGroupMember()
                        {
                            PickemGroupId = group.Id,
                            UserId = synthetic.Id,
                            CreatedBy = group.CommissionerUserId,
                            CreatedUtc = group.CreatedUtc,
                            Role = LeagueRole.Member
                        }, cancellationToken);
                    totalAddedToGroupCount++;
                }
            }

            // Batch save all group member additions
            if (totalAddedToGroupCount > 0)
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Added synthetics to {count} total group memberships.", totalAddedToGroupCount);
            }

            // StatBot's picks follow the matchup previews. The event handlers
            // write them as previews are generated/approved; this sweep is the
            // catch-all for a week that was missed (previews generated while the
            // handlers were down, or before this existed).
            var statbotPicksAdded = await _statBotPickWriter.BackfillWeekAsync(seasonYear, week, cancellationToken);
            _logger.LogInformation("StatBot backfill for {SeasonYear} week {Week}: {Count} pick(s) inserted.", seasonYear, week, statbotPicksAdded);

            // 1. reload all groups
            allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync(cancellationToken);

            // Every synthetic EXCEPT StatBot, whose picks come from the matchup
            // previews above and must not be overwritten by the metric path.
            //
            // Previously filtered on SyntheticPickStyle != null, which silently
            // excluded MetricBot: it is IsSynthetic with a NULL style, so it was
            // added to every league by the loop above and then never given a
            // pick. A null style is not "not a metric bot" — it means "no
            // threshold", i.e. take the model's prediction unmodified.
            var metricBots = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic == true && u.Id != IStatBotPickWriter.StatBotUserId)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Metric pick sweep: {BotCount} synthetic(s) x {GroupCount} league(s) for {SeasonYear} week {Week}.",
                metricBots.Count, allGroups.Count, seasonYear, week);

            var metricContestsWritten = new HashSet<Guid>();

            foreach (var metricBot in metricBots)
            {
                foreach (var group in allGroups)
                {
                    // Per (bot, league) so one bot's bad config cannot cost the
                    // rest their run. An empty style dictionary used to throw
                    // here and the outer catch swallowed it into a SUCCESS
                    // response, so bots later in the loop silently never ran
                    // (prod + local 2026-09-20: MetricBot and NervousBot had
                    // picks, GambleBot and MehBot had none, endpoint said OK).
                    try
                    {
                        var written = await _syntheticPickService.GenerateMetricBasedPicksForSynthetic(
                            group.Id,
                            group.PickType,
                            metricBot.Id,
                            metricBot.SyntheticPickStyle,
                            week,
                            cancellationToken);

                        foreach (var contestId in written)
                            metricContestsWritten.Add(contestId);
                    }
                    // Cancellation is not a per-league failure — continuing
                    // would grind through every remaining (bot x league) after
                    // the caller has already given up.
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Metric picks failed for synthetic {SyntheticId} in group {GroupId} week {Week}; continuing with the rest.",
                            metricBot.Id, group.Id, week);
                    }
                }
            }

            // Picks are scored by ContestFinalized, which has already fired for
            // any week being backfilled and will not fire again — so without
            // this the rows sit unscored forever. PickScoringProcessor
            // short-circuits on "no unscored picks", and these ARE unscored, so
            // it proceeds. Enqueued once per contest rather than once per
            // (bot x league), since scoring handles every pick on the contest.
            foreach (var contestId in metricContestsWritten)
                _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(new ScorePicksCommand(contestId)));

            if (metricContestsWritten.Count > 0)
            {
                _logger.LogInformation(
                    "Metric picks: enqueued scoring for {Count} contest(s).", metricContestsWritten.Count);
            }

            _logger.LogInformation("{method} completed", nameof(RefreshAiExistenceCommandHandler));

            return new Success<Guid>(command.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh AI existence");
            return new Failure<Guid>(
                default,
                ResultStatus.Error,
                new List<ValidationFailure>
                {
                    new ValidationFailure("RefreshAiExistence.Failed", "An error occurred while refreshing AI existence")
                });
        }
    }
}
