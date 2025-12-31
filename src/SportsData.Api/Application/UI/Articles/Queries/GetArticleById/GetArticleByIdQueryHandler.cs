using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Articles.Dtos;
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
    private readonly AppDataContext _dataContext;

    public GetArticleByIdQueryHandler(
        ILogger<GetArticleByIdQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
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
                    ContestId = a.ContestId!.Value,
                    Title = a.Title,
                    Content = a.Content,
                    Url = $"http://localhost:5262/ui/articles/{a.Id}",
                    ImageUrls = a.ImageUrls,
                    GroupSeasonMap = a.FranchiseSeasons.OrderBy(fs => fs.DisplayOrder).FirstOrDefault()!.GroupSeasonMap,
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
