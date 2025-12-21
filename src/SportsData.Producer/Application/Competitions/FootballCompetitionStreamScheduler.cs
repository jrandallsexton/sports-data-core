using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions;

public class FootballCompetitionStreamScheduler
{
    private readonly ILogger<FootballCompetitionStreamScheduler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;

    public FootballCompetitionStreamScheduler(
        ILogger<FootballCompetitionStreamScheduler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
    }

    public async Task Execute()
    {
        // get the current season week
        var seasonWeek = await _dataContext.SeasonWeeks
            .Where(sw => sw.StartDate <= DateTime.UtcNow && sw.EndDate >= DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (seasonWeek == null)
        {
            _logger.LogWarning("No current season week found. Skipping broadcast scheduling.");
            return;
        }

        var contests = await _dataContext.Contests
            .Include(x => x.Competitions)
            .ThenInclude(c => c.ExternalIds)
            .Where(x => x.SeasonWeekId == seasonWeek.Id)
            .AsSplitQuery()
            .ToListAsync();

        var scheduledStreams = await _dataContext.CompetitionStreams
            .Where(x => x.SeasonWeekId == seasonWeek.Id)
            .AsNoTracking()
            .Select(x => x.CompetitionId)
            .ToListAsync();

        var scheduledCount = 0;

        foreach (var contest in contests)
        {
            foreach (var competition in contest.Competitions)
            {
                if (scheduledStreams.Contains(competition.Id))
                {
                    _logger.LogInformation("Stream already scheduled for CompetitionId {CompetitionId}. Skipping.", competition.Id);
                    continue;
                }

                var externalId = competition.ExternalIds.FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);
                if (externalId == null)
                {
                    _logger.LogWarning("No ESPN ExternalId found for CompetitionId {CompetitionId}. Skipping.", competition.Id);
                    continue;
                }

                var scheduledTimeUtc = competition.Date - TimeSpan.FromMinutes(10);
                if (scheduledTimeUtc < DateTime.UtcNow)
                {
                    scheduledTimeUtc = DateTime.UtcNow.AddSeconds(5);
                }

                var correlationId = Guid.NewGuid();

                var jobId = _backgroundJobProvider.Schedule<IFootballCompetitionBroadcastingJob>(
                    job => job.ExecuteAsync(new StreamFootballCompetitionCommand
                    {
                        ContestId = competition.ContestId,
                        CompetitionId = competition.Id,
                        Sport = Sport.FootballNcaa,
                        SeasonYear = contest.SeasonYear,
                        DataProvider = SourceDataProvider.Espn,
                        CorrelationId = correlationId
                    }, CancellationToken.None),
                    scheduledTimeUtc - DateTime.UtcNow);

                _dataContext.CompetitionStreams.Add(new CompetitionStream
                {
                    BackgroundJobId = jobId,
                    CompetitionId = competition.Id,
                    CreatedBy = Guid.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    Id = Guid.NewGuid(),
                    ScheduledBy = nameof(FootballCompetitionStreamScheduler),
                    ScheduledTimeUtc = scheduledTimeUtc,
                    SeasonWeekId = seasonWeek.Id,
                    Status = CompetitionStreamStatus.Scheduled,
                });

                _logger.LogInformation("Scheduled stream for CompetitionId {CompetitionId} at {ScheduledTimeUtc} with JobId {JobId}.",
                    competition.Id, scheduledTimeUtc, jobId);

                scheduledCount++;
            }
        }

        if (scheduledCount > 0)
        {
            await _dataContext.SaveChangesAsync();
            _logger.LogInformation("Scheduled {ScheduledCount} new competition streams for SeasonWeek {SeasonWeekNumber}.",
                scheduledCount, seasonWeek.Number);
        }
        else
        {
            _logger.LogInformation("No new competition streams needed scheduling.");
        }
    }

}