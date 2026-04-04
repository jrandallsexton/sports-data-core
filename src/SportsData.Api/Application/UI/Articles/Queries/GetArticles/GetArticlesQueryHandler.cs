using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportsData.Api.Application.UI.Articles.Dtos;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.UI.Articles.Queries.GetArticles;

public interface IGetArticlesQueryHandler
{
    Task<Result<GetArticlesResponse>> ExecuteAsync(
        GetArticlesQuery query,
        CancellationToken cancellationToken = default);
}

public class GetArticlesQueryHandler : IGetArticlesQueryHandler
{
    private readonly ILogger<GetArticlesQueryHandler> _logger;
    private readonly ApiConfig _config;
    private readonly AppDataContext _dataContext;
    private readonly ISeasonClientFactory _seasonClientFactory;

    public GetArticlesQueryHandler(
        ILogger<GetArticlesQueryHandler> logger,
        IOptions<ApiConfig> config,
        AppDataContext dataContext,
        ISeasonClientFactory seasonClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _dataContext = dataContext;
        _seasonClientFactory = seasonClientFactory;
    }

    public async Task<Result<GetArticlesResponse>> ExecuteAsync(
        GetArticlesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting articles for current season week");

        // TODO: multi-sport — resolve sport from context instead of defaulting
        var weekResult = await _seasonClientFactory.Resolve(Sport.FootballNcaa).GetCurrentSeasonWeek();
        var currentSeasonWeek = weekResult.IsSuccess ? weekResult.Value : null;

        if (currentSeasonWeek is null)
        {
            _logger.LogWarning("Current season week not found");
            return new Failure<GetArticlesResponse>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("SeasonWeek", "Current season week not found.")]);
        }

        var articles = await _dataContext.Articles
            .OrderBy(x => x.Title)
            .Where(x => x.ContestId != null && x.FranchiseSeasons.Any() && x.SeasonWeekId == currentSeasonWeek.Id)
            .Select(a => new ArticleSummaryDto
            {
                ArticleId = a.Id,
                ContestId = a.ContestId,
                Title = a.Title,
                Url = $"{_config.BaseUrl}/ui/articles/{a.Id}",
                ImageUrls = a.ImageUrls,
                GroupSeasonMap = a.FranchiseSeasons.OrderBy(fs => fs.DisplayOrder).FirstOrDefault()!.GroupSeasonMap,
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Found {Count} articles for season week {SeasonWeek}",
            articles.Count,
            currentSeasonWeek.WeekNumber);

        var response = new GetArticlesResponse
        {
            SeasonYear = currentSeasonWeek.SeasonYear,
            SeasonPhase = currentSeasonWeek.SeasonPhase,
            SeasonWeekNumber = currentSeasonWeek.WeekNumber,
            Articles = articles
        };

        return new Success<GetArticlesResponse>(response);
    }
}
