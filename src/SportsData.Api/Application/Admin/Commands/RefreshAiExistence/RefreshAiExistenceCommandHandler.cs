using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Season;

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

    public RefreshAiExistenceCommandHandler(
        ILogger<RefreshAiExistenceCommandHandler> logger,
        AppDataContext dataContext,
        ISeasonClientFactory seasonClientFactory,
        ISyntheticPickService syntheticPickService,
        IStatBotPickWriter statBotPickWriter)
    {
        _logger = logger;
        _dataContext = dataContext;
        _seasonClientFactory = seasonClientFactory;
        _syntheticPickService = syntheticPickService;
        _statBotPickWriter = statBotPickWriter;
    }

    public async Task<Result<Guid>> ExecuteAsync(RefreshAiExistenceCommand command, CancellationToken cancellationToken = default)
    {
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
                        week,
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
