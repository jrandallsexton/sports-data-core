using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.Commands.GenerateGameRecap;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.Contests
{
    public class ContestRecapProcessor
    {
        private readonly ILogger<ContestRecapProcessor> _logger;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly AppDataContext _dataContext;
        private readonly IGenerateGameRecapCommandHandler _generateGameRecapHandler;
        private readonly IEventBus _publishEndpoint;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IContestClientFactory _contestClientFactory;

        public ContestRecapProcessor(
            ILogger<ContestRecapProcessor> logger,
            IProvideCanonicalData canonicalDataProvider,
            AppDataContext dataContext,
            IGenerateGameRecapCommandHandler generateGameRecapHandler,
            IEventBus publishEndpoint,
            IDateTimeProvider dateTimeProvider,
            IContestClientFactory contestClientFactory)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _dataContext = dataContext;
            _generateGameRecapHandler = generateGameRecapHandler;
            _publishEndpoint = publishEndpoint;
            _dateTimeProvider = dateTimeProvider;
            _contestClientFactory = contestClientFactory;
        }

        public async Task ProcessAsync(Guid contestId)
        {
            // TODO: Support multiple sports - pass Sport as parameter
            var contestClient = _contestClientFactory.Resolve(Sport.FootballNcaa);

            // if we already have a recap for this contest, skip processing
            var recapExists = await _dataContext.Articles
                .AnyAsync(x => x.ContestId == contestId);

            if (recapExists)
            {
                _logger.LogInformation("Recap already exists for contest ID {ContestId}, skipping.", contestId);
                return;
            }

            // get the contest overview
            var overview = await contestClient.GetContestOverviewByContestId(contestId);

            // generate the recap using AI handler
            var recapResult = await _generateGameRecapHandler.ExecuteAsync(new GenerateGameRecapCommand
            {
                GameDataJson = overview.ToJson(),
            });

            if (!recapResult.IsSuccess)
            {
                _logger.LogError("Failed to generate recap for contest ID {ContestId}", contestId);
                return;
            }

            var recap = recapResult.Value;

            // save the recap to the database
            var article = new Article
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                Title = recap.Title,
                Content = recap.Recap,
                CreatedUtc = _dateTimeProvider.UtcNow(),
                CreatedBy = new Guid("b6e59053-46cb-4bbf-9876-0a70f539c81c"), // TODO: add synthetic userIds to appConfig
                Tokens = recap.EstimatedPromptTokens,
                TimeMs = recap.GenerationTimeMs,
                AiModel = recap.Model,
                AiPromptNameAndVersion = recap.PromptVersion,
                PublishedAt = _dateTimeProvider.UtcNow(),
                AuthorId = new Guid("b6e59053-46cb-4bbf-9876-0a70f539c81c"),
                SeasonWeekId = overview.Header!.SeasonWeekId,
                SeasonYear = overview.Header!.SeasonYear,
                SeasonWeekNumber = overview.Header!.SeasonWeekNumber,
                FranchiseSeasons = new List<ArticleFranchiseSeason>()
                {
                    new()
                    {
                        FranchiseSeasonId = overview.Header!.AwayTeam!.FranchiseSeasonId,
                        DisplayOrder = 0,
                        GroupSeasonMap = overview.Header!.AwayTeam!.GroupSeasonMap
                    },
                    new()
                    {
                        FranchiseSeasonId = overview.Header!.HomeTeam!.FranchiseSeasonId,
                        DisplayOrder = 1,
                        GroupSeasonMap = overview.Header!.HomeTeam!.GroupSeasonMap
                    }
                },
                ImageUrls =
                [
                    overview.Header!.AwayTeam!.LogoUrl!,
                    overview.Header!.HomeTeam!.LogoUrl!
                ]
            };

            await _dataContext.Articles.AddAsync(article);

            var evt = new ContestRecapArticlePublished(
                contestId,
                article.Id,
                article.Title,
                null,
                Sport.FootballNcaa,
                article.SeasonYear,
                Guid.NewGuid(),
                Guid.NewGuid());
            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();
        }
    }
}
