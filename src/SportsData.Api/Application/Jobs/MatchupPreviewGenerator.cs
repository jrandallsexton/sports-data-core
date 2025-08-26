using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
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
            // get all matchups for the current week
            var matchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();

            var contestIds = matchups.Select(x => x.ContestId);

            // do we want to overwrite existing previews?
            var existingPreviews = await _dataContext.MatchupPreviews
                .Where(x => contestIds.Contains(x.ContestId))
                .AsNoTracking()
                .Select(x => x.ContestId)
                .ToListAsync();

            // enqueue a MatchupPreviewGenerationProcessor for each
            foreach (var matchup in matchups)
            {
                if (existingPreviews.Contains(matchup.ContestId))
                    continue;
                
                var cmd = new GenerateMatchupPreviewsCommand()
                {
                    ContestId = matchup.ContestId
                };

                _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            }
        }
    }
}
