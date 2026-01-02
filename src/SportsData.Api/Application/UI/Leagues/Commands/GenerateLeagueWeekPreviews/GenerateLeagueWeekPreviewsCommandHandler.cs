using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Commands.GenerateLeagueWeekPreviews;

public interface IGenerateLeagueWeekPreviewsCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        GenerateLeagueWeekPreviewsCommand command,
        CancellationToken cancellationToken = default);
}

public class GenerateLeagueWeekPreviewsCommandHandler : IGenerateLeagueWeekPreviewsCommandHandler
{
    private readonly ILogger<GenerateLeagueWeekPreviewsCommandHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public GenerateLeagueWeekPreviewsCommandHandler(
        ILogger<GenerateLeagueWeekPreviewsCommandHandler> logger,
        AppDataContext dbContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        GenerateLeagueWeekPreviewsCommand command,
        CancellationToken cancellationToken = default)
    {
        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == command.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.LeagueId), $"League with ID {command.LeagueId} not found.")]);

        var contestIds = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(x => x.GroupId == command.LeagueId && x.SeasonWeek == command.WeekNumber)
            .Select(x => x.ContestId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Generating previews for {ContestCount} contests in LeagueId={LeagueId}, Week={WeekNumber}",
            contestIds.Count, command.LeagueId, command.WeekNumber);

        var enqueuedCount = 0;
        foreach (var contestId in contestIds)
        {
            var previewExists = await _dbContext.MatchupPreviews
                .Where(x => x.ContestId == contestId && x.RejectedUtc == null)
                .AnyAsync(cancellationToken);

            if (previewExists)
                continue;

            var cmd = new GenerateMatchupPreviewsCommand
            {
                ContestId = contestId
            };
            _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            enqueuedCount++;
        }

        _logger.LogInformation(
            "Enqueued {EnqueuedCount} preview generation jobs for LeagueId={LeagueId}, Week={WeekNumber}",
            enqueuedCount, command.LeagueId, command.WeekNumber);

        return new Success<Guid>(command.LeagueId);
    }
}
