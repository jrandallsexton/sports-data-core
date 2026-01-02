using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.Commands.GenerateGameRecap;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;

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

        /// <summary>
        /// Initializes a new instance of ContestRecapProcessor with required services and data providers.
        /// </summary>
        /// <param name="logger">Logger for informational and error messages.</param>
        /// <param name="canonicalDataProvider">Provides canonical contest overview data used to build recaps.</param>
        /// <param name="dataContext">Entity Framework data context for persisting articles.</param>
        /// <param name="generateGameRecapHandler">Handler that generates AI-based game recap content.</param>
        /// <param name="publishEndpoint">Event bus used to publish domain events after an article is created.</param>
        /// <param name="dateTimeProvider">Provider for the current UTC date/time used when setting timestamps.</param>
        public ContestRecapProcessor(
            ILogger<ContestRecapProcessor> logger,
            IProvideCanonicalData canonicalDataProvider,
            AppDataContext dataContext,
            IGenerateGameRecapCommandHandler generateGameRecapHandler,
            IEventBus publishEndpoint,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _dataContext = dataContext;
            _generateGameRecapHandler = generateGameRecapHandler;
            _publishEndpoint = publishEndpoint;
            _dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Orchestrates generation and publication of a contest recap article for the specified contest.
        /// </summary>
        /// <remarks>
        /// If an article already exists for the contest the method returns without making changes.
        /// Otherwise it obtains the canonical contest overview, requests an AI-generated recap, and on success creates and persists an Article and publishes a ContestRecapArticlePublished event.
        /// If recap generation fails the method returns without creating or publishing an article.
        /// </remarks>
        /// <param name="contestId">The identifier of the contest to process.</param>
        public async Task ProcessAsync(Guid contestId)
        {
            // if we already have a recap for this contest, skip processing
            var recapExists = await _dataContext.Articles
                .AnyAsync(x => x.ContestId == contestId);

            if (recapExists)
            {
                _logger.LogInformation("Recap already exists for contest ID {ContestId}, skipping.", contestId);
                return;
            }

            // get the contest overview
            var overview = await _canonicalDataProvider.GetContestOverviewByContestId(contestId);

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
                Guid.NewGuid(), Guid.NewGuid());
            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();
        }
    }
}