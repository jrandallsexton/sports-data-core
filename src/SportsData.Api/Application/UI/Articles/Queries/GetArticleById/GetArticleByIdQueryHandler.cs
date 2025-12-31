using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Api.Application.UI.Articles.Dtos;
using SportsData.Api.Config;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Articles.Queries.GetArticleById;

public interface IGetArticleByIdQueryHandler
{
    Task<Result<GetArticleResponse>> ExecuteAsync(
        GetArticleByIdQuery query,
        CancellationToken cancellationToken = default);
}

public class GetArticleByIdQueryHandler : IGetArticleByIdQueryHandler
{
    private readonly ILogger<GetArticleByIdQueryHandler> _logger;
    private readonly ApiConfig _config;
    private readonly AppDataContext _dataContext;

    public GetArticleByIdQueryHandler(
        ILogger<GetArticleByIdQueryHandler> logger,
        IOptions<ApiConfig> config,
        AppDataContext dataContext)
    {
        _logger = logger;
        _config = config.Value;
        _dataContext = dataContext;
    }

    public async Task<Result<GetArticleResponse>> ExecuteAsync(
        GetArticleByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting article by id {ArticleId}", query.ArticleId);

        var response = await _dataContext.Articles
            .Where(a => a.Id == query.ArticleId)
            .Select(a => new GetArticleResponse
            {
                Article = new ArticleDto
                {
                    ArticleId = a.Id,
                    ContestId = a.ContestId,
                    Title = a.Title,
                    Content = a.Content,
                    Url = $"{_config.BaseUrl}/ui/articles/{a.Id}",
                    ImageUrls = a.ImageUrls,
                    GroupSeasonMap = a.FranchiseSeasons
                        .OrderBy(fs => fs.DisplayOrder)
                        .Select(fs => fs.GroupSeasonMap)
                        .FirstOrDefault(),
                }
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (response is null)
        {
            _logger.LogWarning("Article {ArticleId} not found", query.ArticleId);
            return new Failure<GetArticleResponse>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.ArticleId), $"Article {query.ArticleId} not found.")]);
        }

        _logger.LogInformation("Found article {ArticleId}", query.ArticleId);

        return new Success<GetArticleResponse>(response);
    }
}
