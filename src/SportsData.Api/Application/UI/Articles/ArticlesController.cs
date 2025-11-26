using Microsoft.AspNetCore.Mvc;

namespace SportsData.Api.Application.UI.Articles
{
    [ApiController]
    [Route("ui/articles")]
    public class ArticlesController : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<GetArticlesResponse>> GetArticles(
            [FromServices] IArticleService articleService)
        {
            var response = await articleService.GetArticlesAsync();
            return Ok(response);
        }
    }
}
