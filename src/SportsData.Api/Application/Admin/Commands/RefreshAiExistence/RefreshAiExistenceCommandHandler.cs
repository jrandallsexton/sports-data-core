using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Commands.RefreshAiExistence;

public interface IRefreshAiExistenceCommandHandler
{
    /// <summary>
/// Ensures synthetic users are members of all pickem groups, creates missing synthetic picks for the current season week, and triggers metric-based pick generation for metric bots.
/// </summary>
/// <param name="command">The refresh command whose CorrelationId will be returned on success.</param>
/// <returns>`Result<Guid>` containing the command CorrelationId on success; on failure contains validation failures describing issues (for example: missing current week, failed matchup retrievals, or other errors encountered while refreshing AI existence).</returns>
Task<Result<Guid>> ExecuteAsync(RefreshAiExistenceCommand command, CancellationToken cancellationToken = default);
}

public class RefreshAiExistenceCommandHandler : IRefreshAiExistenceCommandHandler
{
    private readonly IProvideCanonicalData _canonicalData;
    private readonly AppDataContext _dataContext;
    private readonly IGetLeagueWeekMatchupsQueryHandler _getLeagueWeekMatchupsHandler;
    private readonly ILogger<RefreshAiExistenceCommandHandler> _logger;
    private readonly ISyntheticPickService _syntheticPickService;
    /// <summary>
    /// Initializes a new instance of <see cref="RefreshAiExistenceCommandHandler"/> with its required dependencies.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and operational messages.</param>
    /// <param name="dataContext">Database context for reading and persisting application data.</param>
    /// <param name="canonicalData">Provider for canonical application data such as the current season week.</param>
    /// <param name="syntheticPickService">Service responsible for generating metric-based synthetic picks.</param>
    /// <param name="getLeagueWeekMatchupsHandler">Query handler used to retrieve league week matchups for a given user and league.</param>
    public RefreshAiExistenceCommandHandler(
        ILogger<RefreshAiExistenceCommandHandler> logger,
        AppDataContext dataContext,
        IProvideCanonicalData canonicalData,
        ISyntheticPickService syntheticPickService,
        IGetLeagueWeekMatchupsQueryHandler getLeagueWeekMatchupsHandler)
    {
        _logger = logger;
        _dataContext = dataContext;
        _canonicalData = canonicalData;
        _syntheticPickService = syntheticPickService;
        _getLeagueWeekMatchupsHandler = getLeagueWeekMatchupsHandler;
    }

    /// <summary>
    /// Refresh synthetic users' existence across leagues for the current season week and generate their picks.
    /// </summary>
    /// <param name="command">Command containing the correlation identifier returned on success.</param>
    /// <returns>`command.CorrelationId` wrapped in a success `Result` when processing completes; a failure `Result` when the current week cannot be found (NotFound) or an error occurs during processing.</returns>
    public async Task<Result<Guid>> ExecuteAsync(RefreshAiExistenceCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Accept the seasonWeek as a parameter

            // get the current week
            var currentWeek = await _canonicalData.GetCurrentSeasonWeek();
            //var weeks = await _canonicalData.GetCurrentAndLastWeekSeasonWeeks();
            //var currentWeek = weeks.Where(x => x.WeekNumber == 13).First()!;

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

            // now, for each league, we need to ensure the synthetic has submitted picks
            // those picks will be submitted based on previously-generated MatchupPreview records

            // 1. reload all groups
            allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync(cancellationToken);

            var statbotId = Guid.Parse("5fa4c116-1993-4f2b-9729-c50c62150813");

            var statbotPicksAdded = 0;

            // Create picks for StatBot
            foreach (var group in allGroups)
            {
                // get the matchups for the group
                var query = new GetLeagueWeekMatchupsQuery
                {
                    UserId = statbotId,
                    LeagueId = group.Id,
                    Week = currentWeek.WeekNumber
                };
                var groupMatchupsResult = await _getLeagueWeekMatchupsHandler.ExecuteAsync(query, cancellationToken);

                if (!groupMatchupsResult.IsSuccess)
                {
                    _logger.LogWarning("Could not get matchups for group {GroupId}", group.Id);
                    continue;
                }

                var groupMatchups = groupMatchupsResult.Value;

                // iterate each group matchup
                foreach (var matchup in groupMatchups.Matchups)
                {
                    // get the synthetic's pick
                    var synPick = await _dataContext.UserPicks
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.PickemGroupId == group.Id &&
                                    x.UserId == statbotId)
                        .FirstOrDefaultAsync(cancellationToken);

                    // do we already have one?
                    if (synPick is not null)
                        continue;

                    // get the previously-generated preview
                    var preview = await _dataContext.MatchupPreviews
                        .AsNoTracking()
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.RejectedUtc == null)
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    // no preview? skip it
                    if (preview is null)
                        continue;

                    // generate the synthetic's pick from the preview
                    synPick = new PickemGroupUserPick()
                    {
                        UserId = statbotId,
                        ContestId = matchup.ContestId,
                        CreatedUtc = preview.CreatedUtc,
                        CreatedBy = statbotId,
                        FranchiseId = group.PickType == PickType.AgainstTheSpread
                            ? preview.PredictedSpreadWinner
                            : preview.PredictedStraightUpWinner,
                        PickemGroupId = group.Id,
                        PickType = group.PickType == PickType.StraightUp ? PickType.StraightUp : PickType.AgainstTheSpread,
                        Week = currentWeek.WeekNumber,
                        TiebreakerType = TiebreakerType.TotalPoints
                    };

                    if (group.PickType == PickType.AgainstTheSpread && matchup.SpreadCurrent.HasValue)
                    {
                        synPick.FranchiseId = preview.PredictedSpreadWinner;
                        if (synPick.FranchiseId == Guid.Empty)
                            synPick.FranchiseId = preview.PredictedStraightUpWinner;
                    }
                    else
                    {
                        synPick.FranchiseId = preview.PredictedStraightUpWinner;
                    }

                    await _dataContext.UserPicks.AddAsync(synPick, cancellationToken);
                    statbotPicksAdded++;
                }
            }

            // Batch save all StatBot picks
            if (statbotPicksAdded > 0)
            {
                await _dataContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Created {count} StatBot picks", statbotPicksAdded);
            }

            var metricBots = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic == true && u.SyntheticPickStyle != null)
                .ToListAsync(cancellationToken);

            foreach (var metricBot in metricBots)
            {
                // Create picks for MetricBot
                foreach (var group in allGroups)
                {
                    await _syntheticPickService.GenerateMetricBasedPicksForSynthetic(
                        group.Id,
                        group.PickType,
                        metricBot.Id,
                        metricBot.SyntheticPickStyle!,
                        currentWeek.WeekNumber,
                        cancellationToken);
                }
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