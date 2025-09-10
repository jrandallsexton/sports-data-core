using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Previews
{
    public class MatchupPreviewGenerator
    {
        private readonly ILogger<MatchupPreviewGenerator> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public MatchupPreviewGenerator(
            ILogger<MatchupPreviewGenerator> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // Get all matchups for the current week
            var matchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();
            var contestIds = matchups.Select(x => x.ContestId).ToHashSet();

            // Fetch existing previews (including their validation status)
            var existingPreviews = await _dataContext.MatchupPreviews
                .Where(x => contestIds.Contains(x.ContestId) && x.RejectedUtc != null)
                .AsNoTracking()
                .ToDictionaryAsync(x => x.ContestId, x => x.ValidationErrors);

            // Identify league-relevant contests
            var currentWeekId = matchups.First().SeasonWeekId;

            var leagueContestIds = await _dataContext.PickemGroupMatchups
                .Where(x => x.SeasonWeekId == currentWeekId)
                .AsNoTracking()
                .Select(x => x.ContestId)
                .Distinct()
                .ToListAsync();

            var leagueMatchupCount = 0;

            // === 1. Process league matchups first ===
            foreach (var matchup in matchups.Where(x => leagueContestIds.Contains(x.ContestId)))
            {
                if (ShouldSkip(existingPreviews, matchup.ContestId))
                    continue;

                Enqueue(matchup.ContestId);

                leagueMatchupCount++;
            }

            // === 2. Process non-league matchups second ===
            foreach (var matchup in matchups.Where(x => !leagueContestIds.Contains(x.ContestId)))
            {
                if (ShouldSkip(existingPreviews, matchup.ContestId))
                    continue;

                Enqueue(matchup.ContestId);
            }

            void Enqueue(Guid contestId)
            {
                var cmd = new GenerateMatchupPreviewsCommand
                {
                    ContestId = contestId
                };

                // TODO: Replace with prioritized queue via Service Bus
                _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            }

            static bool ShouldSkip(Dictionary<Guid, string?> previews, Guid contestId)
            {
                return previews.TryGetValue(contestId, out var errors) && string.IsNullOrWhiteSpace(errors);
            }

            _logger.LogInformation("Enqueued {LeagueCount} league matchups for preview generation.", leagueMatchupCount);
        }

    }
}
