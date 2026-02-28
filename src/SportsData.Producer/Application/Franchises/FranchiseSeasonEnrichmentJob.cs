using Microsoft.EntityFrameworkCore;

using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Franchises
{
    public class FranchiseSeasonEnrichmentJob
    {
        private readonly ILogger<FranchiseSeasonEnrichmentJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FranchiseSeasonEnrichmentJob(
            ILogger<FranchiseSeasonEnrichmentJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync(int? seasonYear = null)
        {
            var effectiveSeasonYear = seasonYear ?? DateTime.UtcNow.Year;
            
            var franchiseSeasons = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(x => x.SeasonYear == effectiveSeasonYear)
                .ToListAsync();

            _logger.LogInformation("Requesting enrichment for {count} franchise seasons for year {seasonYear}.", franchiseSeasons.Count, effectiveSeasonYear);

            foreach (var franchiseSeason in franchiseSeasons)
            {
                var cmd = new EnrichFranchiseSeasonCommand(
                    franchiseSeason.Id,
                    effectiveSeasonYear,
                    Guid.NewGuid());

                _backgroundJobProvider
                    .Enqueue<EnrichFranchiseSeasonHandler<TeamSportDataContext>>(p => p.Process(cmd));
            }

            _logger.LogInformation("All franchise season enrichment requests sent.");
        }
    }
}
