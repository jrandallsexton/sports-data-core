using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Articles
{
    public interface IArticleService
    {
        Task<GetArticlesResponse> GetArticlesAsync();
        Task<GetArticleResponse> GetArticleByIdAsync(Guid id);
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
                .OrderBy(x => x.Title)
                .Select(a => new ArticleSummaryDto
                {
                    ArticleId = a.Id,
                    ContestId = a.ContestId!.Value,
                    Title = a.Title,
                    Url = $"http://localhost:5262/ui/articles/{a.Id}",
                    ImageUrls = a.ImageUrls
                })
                .ToListAsync();

            return new GetArticlesResponse()
            {
                Articles = articles
            };
        }

        public Task<GetArticleResponse> GetArticleByIdAsync(Guid id)
        {
            return _dataContext.Articles
                .Where(a => a.Id == id)
                .Select(a => new GetArticleResponse
                {
                    Article = new ArticleDto
                    {
                        ArticleId = a.Id,
                        ContestId = a.ContestId!.Value,
                        Title = a.Title,
                        Content = a.Content,
                        Url = $"http://localhost:5262/ui/articles/{a.Id}",
                        ImageUrls = a.ImageUrls
                    }
                })
                .FirstOrDefaultAsync()!; }
    }

    public class GetArticleResponse
    {
        public ArticleDto Article { get; set; } = null!;
    }

    public class GetArticlesResponse
    {
        public List<ArticleSummaryDto> Articles { get; set; } = [];
    }

    public class ArticleDto
    {
        public Guid ArticleId { get; set; }

        public Guid ContestId { get; set; }

        public Guid? AwayFranchiseSeasonId { get; set; }

        public Guid? HomeFranchiseSeasonId { get; set; }

        public required string Title { get; set; }

        public required string Content { get; set; }

        public required string Url { get; set; }

        public string[] ImageUrls { get; set; } = [];
    }

    public class ArticleSummaryDto
    {
        public Guid ArticleId { get; set; }

        public Guid ContestId { get; set; }

        public Guid? AwayFranchiseSeasonId { get; set; }

        public Guid? HomeFranchiseSeasonId { get; set; }

        public string[] ImageUrls { get; set; } = [];

        public required string Title { get; set; }

        public required string Url { get; set; }
    }
}
