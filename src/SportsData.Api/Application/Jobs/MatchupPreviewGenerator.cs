using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    public class MatchupPreviewGenerator
    {
        private readonly AppDataContext _dataContext;
        private readonly ILogger<MatchupPreviewGenerator> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public MatchupPreviewGenerator(
            AppDataContext dataContext,
            ILogger<MatchupPreviewGenerator> logger,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _dataContext = dataContext;
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get all matchups for the current week
            var matchups = await _canonicalDataProvider.GetMatchupsForCurrentWeek();

            // enqueue a MatchupPreviewGenerationProcessor for each
            foreach (var matchup in matchups)
            {
                var cmd = new GenerateMatchupPreviewsCommand()
                {
                    ContestId = matchup.ContestId
                };

                _backgroundJobProvider.Enqueue<MatchupPreviewProcessor>(p => p.Process(cmd));
            }
        }
    }
}
