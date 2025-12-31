using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.UI.Articles.Dtos;
using SportsData.Api.Application.UI.Articles.Queries.GetArticleById;
using SportsData.Api.Application.UI.Articles.Queries.GetArticles;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.UI.Articles;

[ApiController]
[Route("ui/articles")]
public class ArticlesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetArticlesResponse>> GetArticles(
        [FromServices] IGetArticlesQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetArticlesQuery();
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GetArticleResponse>> GetArticleById(
        [FromRoute] Guid id,
        [FromServices] IGetArticleByIdQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var query = new GetArticleByIdQuery { ArticleId = id };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
