using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Articles.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

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
    private readonly AppDataContext _dataContext;
    private readonly IProvideCanonicalData _canonicalDataProvider;

    public GetArticlesQueryHandler(
        ILogger<GetArticlesQueryHandler> logger,
        AppDataContext dataContext,
        IProvideCanonicalData canonicalDataProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _canonicalDataProvider = canonicalDataProvider;
    }

    public async Task<Result<GetArticlesResponse>> ExecuteAsync(
        GetArticlesQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting articles for current season week");

        var currentSeasonWeek = await _canonicalDataProvider.GetCurrentSeasonWeek();

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
            .Where(x => x.SeasonWeekId == currentSeasonWeek.Id)
            .Select(a => new ArticleSummaryDto
            {
                ArticleId = a.Id,
                ContestId = a.ContestId!.Value,
                Title = a.Title,
                Url = $"http://localhost:5262/ui/articles/{a.Id}",
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
