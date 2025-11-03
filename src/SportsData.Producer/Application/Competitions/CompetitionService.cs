using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.YouTube;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Competitions
{
    public interface ICompetitionService
    {
        Task<Result<Guid>> RefreshCompetitionDrives(Guid competitionId);
        Task RefreshCompetitionMetrics(int seasonYear);
        Task RefreshCompetitionMedia(int seasonYear);
        Task RefreshCompetitionMedia(Guid competitionId, bool removeExisting = false);
    }

    public class CompetitionService : ICompetitionService
    {
        private readonly TeamSportDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IProvideYouTube _youTubeProvider;
        private readonly IGroupSeasonsService _groupSeasonsService;

        private const int ExpectedCompetitionMetricsCount = 2; // both teams should have metrics

        public CompetitionService(
            TeamSportDataContext dbContext,
            IEventBus eventBus,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideBackgroundJobs backgroundJobProvider,
            IProvideYouTube youTubeProvider,
            IGroupSeasonsService groupSeasonsService)
        {
            _dataContext = dbContext;
            _eventBus = eventBus;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _backgroundJobProvider = backgroundJobProvider;
            _youTubeProvider = youTubeProvider;
            _groupSeasonsService = groupSeasonsService;
        }

        public async Task<Result<Guid>> RefreshCompetitionDrives(Guid competitionId)
        {
            var competition = await _dataContext.Competitions
                .Include(x => x.ExternalIds.Where(y => y.CompetitionId == competitionId))
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition is null)
            {
                return new Failure<Guid>(
                    competitionId,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(competitionId), "Competition Not Found")]
                );
            }

            var competitionExternalId = competition.ExternalIds
                .FirstOrDefault();

            if (competitionExternalId is null)
            {
                return new Failure<Guid>(
                    competitionId,
                    ResultStatus.NotFound,
                    [new ValidationFailure(nameof(competitionId), "Competition ExternalId Not Found")]
                );
            }

            var sourceUrl = new Uri(competitionExternalId.SourceUrl);

            var drivesRef = EspnUriMapper.CompetitionRefToCompetitionDrivesRef(sourceUrl);
            var drivesIdentity = _externalRefIdentityGenerator.Generate(drivesRef);

            // request sourcing?
            await _eventBus.Publish(new DocumentRequested(
                Id: drivesIdentity.UrlHash,
                ParentId: competitionId.ToString(),
                Uri: new Uri(drivesIdentity.CleanUrl),
                Sport: Sport.FootballNcaa, // TODO: remove hard-coding
                SeasonYear: 2025, // TODO: remove hard-coding
                DocumentType: DocumentType.EventCompetitionDrive,
                SourceDataProvider: SourceDataProvider.Espn, // TODO: remove hard-coding
                CorrelationId: Guid.NewGuid(),
                CausationId: CausationId.Producer.CompetitionService
            ));

            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            return new Success<Guid>(competitionId, ResultStatus.Accepted);
        }

        public async Task RefreshCompetitionMetrics(int seasonYear)
        {
            var contests = await _dataContext.Contests
                .Include(x => x.Competitions)
                .ThenInclude(comp => comp.Metrics)
                .Where(c => c.FinalizedUtc != null && c.SeasonYear == seasonYear)
                .OrderBy(c => c.StartDateUtc)
                .AsSplitQuery()
                .ToListAsync();

            foreach (var contest in contests)
            {
                var competition = contest.Competitions.FirstOrDefault();

                if (competition is null)
                    continue;

                if (competition.Metrics.Count == ExpectedCompetitionMetricsCount)
                    continue;

                _backgroundJobProvider.Enqueue<ICompetitionMetricService>(p =>
                    p.CalculateCompetitionMetrics(competition.Id));
            }
        }

        public async Task RefreshCompetitionMedia(int seasonYear)
        {
            var fbsGroupIds = await _groupSeasonsService.GetFbsGroupSeasonIds(seasonYear);

            var competitionIds = await _dataContext.Competitions
                .Include(c => c.Contest)
                .ThenInclude(contest => contest.AwayTeamFranchiseSeason)
                .Include(c => c.Contest)
                .ThenInclude(contest => contest.HomeTeamFranchiseSeason)
                .AsNoTracking()
                .Where(c => c.Contest.FinalizedUtc != null &&
                            !c.Media.Any() &&
                            (fbsGroupIds.Contains(c.Contest.AwayTeamFranchiseSeason.GroupSeasonId!.Value) ||
                             fbsGroupIds.Contains(c.Contest.HomeTeamFranchiseSeason.GroupSeasonId!.Value)))
                .OrderByDescending(x => x.Contest.StartDateUtc)
                .Select(c => c.Id)
                .ToListAsync();

            foreach (var competitionId in competitionIds)
            {
                // enqueue Hangfire job here
                _backgroundJobProvider.Enqueue<ICompetitionService>(p =>
                    p.RefreshCompetitionMedia(competitionId, false));
            }
        }

        public async Task RefreshCompetitionMedia(Guid competitionId, bool removeExisting = false)
        {

            if (removeExisting)
            {
                await _dataContext.CompetitionMedia
                    .Where(cm => cm.CompetitionId == competitionId)
                    .ExecuteDeleteAsync();
            }

            var competition = await _dataContext.Competitions
                .Include(x => x.Contest)
                .FirstOrDefaultAsync(c => c.Id == competitionId);

            if (competition is null)
                return;

            var searchTerm = $"{competition.Contest.Name} {competition.Contest.SeasonYear} Highlights";

            var youTubeResults = await _youTubeProvider.Search(searchTerm);

            if (youTubeResults != null && youTubeResults.Items.Any())
            {
                foreach (var mediaEntity in youTubeResults.Items.Select(ytResult => new CompetitionMedia()
                         {
                             Id = Guid.NewGuid(),
                             AwayFranchiseSeasonId = competition.Contest.AwayTeamFranchiseSeasonId,
                             ChannelId = ytResult.Snippet.ChannelId,
                             ChannelTitle = ytResult.Snippet.ChannelTitle,
                             CompetitionId = competition.Id,
                             CreatedBy = Guid.Empty,
                             CreatedUtc = DateTime.UtcNow,
                             Description = ytResult.Snippet.Description,
                             HomeFranchiseSeasonId = competition.Contest.HomeTeamFranchiseSeasonId,
                             PublishedUtc = ytResult.Snippet.PublishedAt,
                             ThumbnailDefaultHeight = ytResult.Snippet.Thumbnails.Default.Height,
                             ThumbnailDefaultUrl = ytResult.Snippet.Thumbnails.Default.Url,
                             ThumbnailDefaultWidth = ytResult.Snippet.Thumbnails.Default.Width,
                             ThumbnailHighHeight = ytResult.Snippet.Thumbnails.High.Height,
                             ThumbnailHighUrl = ytResult.Snippet.Thumbnails.High.Url,
                             ThumbnailHighWidth = ytResult.Snippet.Thumbnails.High.Width,
                             ThumbnailMediumHeight = ytResult.Snippet.Thumbnails.Medium.Height,
                             ThumbnailMediumUrl = ytResult.Snippet.Thumbnails.Medium.Url,
                             ThumbnailMediumWidth = ytResult.Snippet.Thumbnails.Medium.Width,
                             Title = ytResult.Snippet.Title,
                             VideoId = ytResult.Id.VideoId
                         }))
                {
                    _dataContext.CompetitionMedia.Add(mediaEntity);
                }

                await _dataContext.SaveChangesAsync();
            }
        }
    }
}
