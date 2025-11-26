using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Articles
{
    public interface IArticleService
    {
        Task<GetArticlesResponse> GetArticlesAsync();
    }

    public class ArticleService : IArticleService
    {
        private readonly ILogger<ArticleService> _logger;
        private readonly AppDataContext _dataContext;

        public ArticleService(
            ILogger<ArticleService> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task<GetArticlesResponse> GetArticlesAsync()
        {
            var articles = await _dataContext.Articles
                .Select(a => new ArticleDto
                {
                    ContestId = a.ContestId!.Value,
                    Title = a.Title,
                    Url = "https://google.com"
                })
                .ToListAsync();

            return new GetArticlesResponse()
            {
                Articles = articles
            };
        }
    }

    public class GetArticlesResponse
    {
        public List<ArticleDto> Articles { get; set; } = [];
    }

    public class ArticleDto
    {
        public Guid ContestId { get; set; }

        public Guid? AwayFranchiseSeasonId { get; set; }

        public Guid? HomeFranchiseSeasonId { get; set; }

        public required string Title { get; set; }

        public required string Url { get; set; }
    }
}
