using Microsoft.EntityFrameworkCore;

using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises
{
    public class FranchiseSeasonEnrichmentJob
    {
        private readonly ILogger<FranchiseSeasonEnrichmentJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        const int SEASON_YEAR = 2025; // TODO: My configurable or dynamic

        public FranchiseSeasonEnrichmentJob(
            ILogger<FranchiseSeasonEnrichmentJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            var franchiseSeasons = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(x => x.SeasonYear == SEASON_YEAR &&
                            (x.ModifiedUtc == null ||
                            x.ModifiedUtc < DateTime.UtcNow.AddHours(-24)))
                .ToListAsync();

            _logger.LogInformation("Requesting enrichment for {count} franchise seasons.", franchiseSeasons.Count);

            foreach (var franchiseSeason in franchiseSeasons)
            {
                var cmd = new EnrichFranchiseSeasonCommand(
                    franchiseSeason.Id,
                    SEASON_YEAR,
                    Guid.NewGuid());

                _backgroundJobProvider
                    .Enqueue<FranchiseSeasonEnrichmentProcessor<TeamSportDataContext>>(p => p.Process(cmd));
            }

            _logger.LogInformation("All franchise season enrichment requests sent.");
        }
    }
}
