using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Admin
{
    public interface IAdminService
    {
        Task RefreshAiExistence(Guid correlationId);

        Task AuditAi(Guid correlationId);
    }

    public class AdminService : IAdminService
    {
        private readonly ILogger<AdminService> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        private readonly ILeagueService _leagueService;

        public AdminService(
            ILogger<AdminService> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            ILeagueService leagueService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _leagueService = leagueService;
        }

        public async Task RefreshAiExistence(Guid correlationId)
        {
            const int WEEK = 1;

            // get the synthetic; there is only one now
            var synthetic = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic)
                .FirstAsync();
            
            // get all pickemGroups
            var allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync();

            var addedToGroupCount = 0;

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
                    });
                await _dataContext.SaveChangesAsync();
                addedToGroupCount++;
            }

            _logger.LogWarning("Added synthetic to {count} groups.", addedToGroupCount);

            // now, for each league, we need to ensure the synthetic has submitted picks
            // those picks will be submitted based on previously-generated MatchupPreview records

            // 1. reload all groups
            allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync();

            foreach (var group in allGroups)
            {
                // get the matchups for the group
                var groupMatchups = await _leagueService
                        .GetMatchupsForLeagueWeekAsync(synthetic.Id, group.Id, WEEK, CancellationToken.None);

                // iterate each group matchup
                foreach (var matchup in groupMatchups.Matchups)
                {
                    // get the synthetic's pick
                    var synPick = await _dataContext.UserPicks
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.PickemGroupId == group.Id &&
                                    x.UserId == synthetic.Id)
                        .FirstOrDefaultAsync();

                    // do we already have one?
                    if (synPick is not null)
                        continue;

                    // get the previously-generated preview
                    var preview = await _dataContext.MatchupPreviews
                        .AsNoTracking()
                        .Where(x => x.ContestId == matchup.ContestId)
                        .FirstOrDefaultAsync();

                    // no preview? skip it
                    if (preview is null)
                        continue;

                    // generate the synthetic's pick from the preview
                    synPick = new PickemGroupUserPick()
                    {
                        UserId = synthetic.Id,
                        ContestId = matchup.ContestId,
                        CreatedUtc = preview.CreatedUtc,
                        CreatedBy = synthetic.Id,
                        FranchiseId = group.PickType == PickType.AgainstTheSpread
                            ? preview.PredictedSpreadWinner
                            : preview.PredictedStraightUpWinner,
                        PickemGroupId = group.Id,
                        PickType = UserPickType.StraightUp,
                        Week = WEEK,
                        TiebreakerType = TiebreakerType.TotalPoints
                    };

                    if (group.PickType == PickType.AgainstTheSpread && matchup.HomeSpread.HasValue)
                    {
                        synPick.FranchiseId = preview.PredictedSpreadWinner;
                        if (synPick.FranchiseId == Guid.Empty)
                            synPick.FranchiseId = preview.PredictedStraightUpWinner;
                    }
                    else
                    {
                        synPick.FranchiseId = preview.PredictedStraightUpWinner;
                    }

                    await _dataContext.UserPicks.AddAsync(synPick);
                    await _dataContext.SaveChangesAsync();
                }
            }

            _logger.LogInformation("{method} completed", nameof(RefreshAiExistence));
        }

        /// <summary>
        /// MatchupsPreviews whose prediction was correct based on the narrative,
        /// but the model hallucinated FranchiseSeasonId
        /// resulting in an incorrect pick and the wrong scoring for accuracy
        /// </summary>
        /// <param name="correlationId"></param>
        /// <returns></returns>
        public async Task AuditAi(Guid correlationId)
        {
            // load all previews
            var previews = await _dataContext.MatchupPreviews
                .ToListAsync();

            var contestsInGroups = await _dataContext.PickemGroupMatchups
                .Select(x => x.ContestId)
                .Distinct()
                .ToListAsync();

            previews = previews.Where(x => contestsInGroups.Contains(x.ContestId)).ToList();

            var errorCount = 0;

            foreach (var preview in previews)
            {
                // get the matchup used to generate the preview
                var matchup = await _canonicalData.GetMatchupForPreview(preview.ContestId);

                if (matchup is null)
                {
                    _logger.LogCritical("Matchup not found for previewId {previewId}", preview.Id);
                    errorCount++;
                    continue;
                }

                if (preview.PredictedStraightUpWinner != matchup.AwayFranchiseSeasonId &&
                    preview.PredictedStraightUpWinner != matchup.HomeFranchiseSeasonId)
                {
                    // AI hallucinated the winning franchiseSeasonId
                    _logger.LogCritical("AI hallucinated the winning franchiseSeasonId for {previewId}", preview.Id);
                    errorCount++;
                }

                if (matchup.HomeSpread.HasValue)
                {
                    if (!preview.PredictedSpreadWinner.HasValue)
                    {
                        _logger.LogCritical("Matchup had a spread but AI did not generate one for previewId {previewId}", preview.Id);
                        errorCount++;
                        continue;
                    }

                    if (preview.PredictedSpreadWinner != matchup.AwayFranchiseSeasonId &&
                        preview.PredictedSpreadWinner != matchup.HomeFranchiseSeasonId)
                    {
                        // AI hallucinated the FranchiseSeasonId of the spread winner
                        _logger.LogCritical("AI hallucinated the spread winning franchiseSeasonId for {previewId}", preview.Id);
                        errorCount++;
                    }
                }
            }

            _logger.LogCritical($"!!! {errorCount} of {previews.Count} AI previews have issues with FranchiseSeasonId !!!");
        }
    }
}
