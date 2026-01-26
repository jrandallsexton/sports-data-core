using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;

public interface IRefreshAllCompetitionMediaCommandHandler
{
    Task<Result<RefreshAllCompetitionMediaResult>> ExecuteAsync(
        RefreshAllCompetitionMediaCommand command,
        CancellationToken cancellationToken = default);
}

public class RefreshAllCompetitionMediaCommandHandler : IRefreshAllCompetitionMediaCommandHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IGroupSeasonsService _groupSeasonsService;

    public RefreshAllCompetitionMediaCommandHandler(
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IGroupSeasonsService groupSeasonsService)
    {
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _groupSeasonsService = groupSeasonsService;
    }

    public async Task<Result<RefreshAllCompetitionMediaResult>> ExecuteAsync(
        RefreshAllCompetitionMediaCommand command,
        CancellationToken cancellationToken = default)
    {
        List<Guid> competitionIds;

        if (command.Sport == Sport.FootballNcaa)
        {
            // FootballNcaa: Apply FBS filtering
            var fbsGroupIds = await _groupSeasonsService.GetFbsGroupSeasonIds(command.SeasonYear);

            competitionIds = await _dataContext.Competitions
                .Include(c => c.Contest)
                .ThenInclude(contest => contest.AwayTeamFranchiseSeason)
                .Include(c => c.Contest)
                .ThenInclude(contest => contest.HomeTeamFranchiseSeason)
                .AsNoTracking()
                .Where(c => c.Contest.SeasonYear == command.SeasonYear &&
                            c.Contest.FinalizedUtc != null &&
                            !c.Media.Any() &&
                            ((c.Contest.AwayTeamFranchiseSeason.GroupSeasonId.HasValue &&
                              fbsGroupIds.Contains(c.Contest.AwayTeamFranchiseSeason.GroupSeasonId.Value)) ||
                             (c.Contest.HomeTeamFranchiseSeason.GroupSeasonId.HasValue &&
                              fbsGroupIds.Contains(c.Contest.HomeTeamFranchiseSeason.GroupSeasonId.Value))))
                .OrderByDescending(x => x.Contest.StartDateUtc)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // All other sports: No FBS filtering, just get competitions without media
            competitionIds = await _dataContext.Competitions
                .Include(c => c.Contest)
                .AsNoTracking()
                .Where(c => c.Contest.SeasonYear == command.SeasonYear &&
                            c.Contest.Sport == command.Sport &&
                            c.Contest.FinalizedUtc != null &&
                            !c.Media.Any())
                .OrderByDescending(x => x.Contest.StartDateUtc)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }

        var enqueuedCount = 0;

        foreach (var competitionId in competitionIds)
        {
            var mediaCommand = new RefreshCompetitionMediaCommand(competitionId, RemoveExisting: false);
            _backgroundJobProvider.Enqueue<IRefreshCompetitionMediaCommandHandler>(h =>
                h.ExecuteAsync(mediaCommand, CancellationToken.None));

            enqueuedCount++;
        }

        var result = new RefreshAllCompetitionMediaResult(
            command.Sport,
            command.SeasonYear,
            competitionIds.Count,
            enqueuedCount,
            $"Enqueued {enqueuedCount} media refresh jobs for {competitionIds.Count} competitions in season {command.SeasonYear}");

        return new Success<RefreshAllCompetitionMediaResult>(result);
    }
}
