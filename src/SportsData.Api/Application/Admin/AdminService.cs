using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Canonical.Models;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using System.Text.Json.Serialization;

using Twilio.Annotations;

namespace SportsData.Api.Application.Admin
{
    public interface IAdminService
    {
        Task RefreshAiExistence(Guid correlationId);

        Task AuditAi(Guid correlationId);

        Task<Result<string>> GetMatchupPreview(Guid contestId);

        Task<Result<Guid>> UpsertMatchupPreview(string jsonContent);

        Task<Result<GetCompetitionsWithoutCompetitorsResponse>> GetCompetitionsWithoutCompetitors();
        
        Task<Result<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays();
        
        Task<Result<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives();
    }

    public class AdminService : IAdminService
    {
        private readonly ILogger<AdminService> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        private readonly ILeagueService _leagueService;
        private readonly IProvideCanonicalAdminData _canonicalAdminData;

        public AdminService(
            ILogger<AdminService> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            ILeagueService leagueService,
            IProvideCanonicalAdminData canonicalAdminData)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _leagueService = leagueService;
            _canonicalAdminData = canonicalAdminData;
        }

        public async Task RefreshAiExistence(Guid correlationId)
        {
            // TODO: Accept the seasonWeek as a parameter

            // get the current week
            var currentWeek = await _canonicalData.GetCurrentSeasonWeek();
            //var weeks = await _canonicalData.GetCurrentAndLastWeekSeasonWeeks();
            //var currentWeek = weeks.Where(x => x.WeekNumber == 13).First()!;

            if (currentWeek is null)
            {
                _logger.LogError("Current week could not be found");
                throw new Exception("Current week could not be found");
            }

            // get the synthetics
            var synthetics = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic)
                .ToListAsync();
            
            // get all pickemGroups
            var allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync();

            foreach (var synthetic in synthetics)
            {
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
            }

            // now, for each league, we need to ensure the synthetic has submitted picks
            // those picks will be submitted based on previously-generated MatchupPreview records

            // 1. reload all groups
            allGroups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .ToListAsync();

            var statbotId = Guid.Parse("5fa4c116-1993-4f2b-9729-c50c62150813");

            // Create picks for StatBot
            foreach (var group in allGroups)
            {
                // get the matchups for the group
                var groupMatchupsResult = await _leagueService
                        .GetMatchupsForLeagueWeekAsync(statbotId, group.Id, currentWeek.WeekNumber, CancellationToken.None);

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
                        .FirstOrDefaultAsync();

                    // do we already have one?
                    if (synPick is not null)
                        continue;

                    // get the previously-generated preview
                    var preview = await _dataContext.MatchupPreviews
                        .AsNoTracking()
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.RejectedUtc == null)
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefaultAsync();

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
                        PickType = group.PickType == PickType.StraightUp ? UserPickType.StraightUp : UserPickType.AgainstTheSpread,
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

                    await _dataContext.UserPicks.AddAsync(synPick);
                    await _dataContext.SaveChangesAsync();
                }
            }

            var metricBotId = Guid.Parse("b210d677-19c3-4f26-ac4b-b2cc7ad58c44");

            // Create picks for MetricBot
            foreach (var group in allGroups)
            {
                // get the matchups for the group
                var groupMatchupsResult = await _leagueService
                    .GetMatchupsForLeagueWeekAsync(statbotId, group.Id, currentWeek.WeekNumber, CancellationToken.None);

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
                                    x.UserId == metricBotId)
                        .FirstOrDefaultAsync();

                    // do we already have one?
                    if (synPick is not null)
                        continue;

                    // get the previously-generated ContestPrediction
                    var prediction = await _dataContext.ContestPredictions
                        .AsNoTracking()
                        .Where(x => x.ContestId == matchup.ContestId &&
                                    x.PredictionType == group.PickType)
                        .OrderByDescending(x => x.CreatedUtc)
                        .FirstOrDefaultAsync();

                    // no preview? skip it
                    if (prediction is null)
                        continue;

                    // generate the synthetic's pick from the ContestPrediction
                    synPick = new PickemGroupUserPick()
                    {
                        UserId = metricBotId,
                        ContestId = matchup.ContestId,
                        CreatedUtc = prediction.CreatedUtc,
                        CreatedBy = metricBotId,
                        FranchiseId = prediction.WinnerFranchiseSeasonId,
                        PickemGroupId = group.Id,
                        PickType = prediction.PredictionType == PickType.StraightUp ?
                            UserPickType.StraightUp : UserPickType.AgainstTheSpread,
                        Week = currentWeek.WeekNumber,
                        TiebreakerType = TiebreakerType.TotalPoints
                    };

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

        public async Task<Result<string>> GetMatchupPreview(Guid contestId)
        {
            if (contestId == Guid.Empty)
            {
                return new Failure<string>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(contestId), "Contest ID cannot be empty")]);
            }

            try
            {
                var preview = await _dataContext.MatchupPreviews
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ContestId == contestId);

                if (preview is null)
                {
                    _logger.LogWarning("No preview found for contest {ContestId}", contestId);
                    return new Failure<string>(
                        default!,
                        ResultStatus.NotFound,
                        [new ValidationFailure(nameof(contestId), "No preview found for the specified contest")]);
                }

                return new Success<string>(preview.ToJson());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matchup preview for contest {ContestId}", contestId);
                return new Failure<string>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(contestId), $"Error retrieving preview: {ex.Message}")]);
            }
        }

        public async Task<Result<Guid>> UpsertMatchupPreview(string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new Failure<Guid>(
                    default,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(jsonContent), "JSON content cannot be empty")]);
            }

            try
            {
                var preview = jsonContent.FromJson<MatchupPreview>();

                if (preview is null)
                {
                    _logger.LogWarning("Invalid preview content provided");
                    return new Failure<Guid>(
                        default,
                        ResultStatus.Validation,
                        [new ValidationFailure(nameof(jsonContent), "Invalid preview content")]);
                }

                var existing = await _dataContext.MatchupPreviews
                    .FirstOrDefaultAsync(x => x.ContestId == preview.ContestId);

                if (existing is not null)
                    _dataContext.MatchupPreviews.Remove(existing);

                await _dataContext.MatchupPreviews.AddAsync(preview);
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation("Upserted matchup preview for contest {ContestId}", preview.ContestId);

                return new Success<Guid>(preview.ContestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting matchup preview");
                return new Failure<Guid>(
                    default,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(jsonContent), $"Error upserting preview: {ex.Message}")]);
            }
        }

        public async Task<Result<GetCompetitionsWithoutCompetitorsResponse>> GetCompetitionsWithoutCompetitors()
        {
            try
            {
                var result = await _canonicalAdminData.GetCompetitionsWithoutCompetitors();
                return new Success<GetCompetitionsWithoutCompetitorsResponse>(new GetCompetitionsWithoutCompetitorsResponse
                {
                    Items = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get competitions without competitors");
                return new Failure<GetCompetitionsWithoutCompetitorsResponse>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure("competitions", $"Error retrieving competitions without competitors: {ex.Message}")]);
            }
        }

        public async Task<Result<List<CompetitionWithoutPlaysDto>>> GetCompetitionsWithoutPlays()
        {
            try
            {
                var result = await _canonicalAdminData.GetCompetitionsWithoutPlays();
                return new Success<List<CompetitionWithoutPlaysDto>>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get competitions without plays");
                return new Failure<List<CompetitionWithoutPlaysDto>>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure("competitions", $"Error retrieving competitions without plays: {ex.Message}")]);
            }
        }

        public async Task<Result<List<CompetitionWithoutDrivesDto>>> GetCompetitionsWithoutDrives()
        {
            try
            {
                var result = await _canonicalAdminData.GetCompetitionsWithoutDrives();
                return new Success<List<CompetitionWithoutDrivesDto>>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get competitions without drives");
                return new Failure<List<CompetitionWithoutDrivesDto>>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure("competitions", $"Error retrieving competitions without drives: {ex.Message}")]);
            }
        }
    }

    public class RejectMatchupPreviewCommand
    {
        [JsonPropertyName("previewId")]
        public Guid PreviewId { get; set; }

        [JsonPropertyName("contestId")]
        public Guid ContestId { get; set; }

        [JsonPropertyName("rejectionNote")]
        public required string RejectionNote { get; set; }

        public Guid RejectedByUserId { get; set; }
    }

    public class ApproveMatchupPreviewCommand
    {
        [JsonPropertyName("previewId")]
        public Guid PreviewId { get; set; }

        public Guid ApprovedByUserId { get; set; }
    }
}
