using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application.UI.Articles.Queries.GetArticleById;
using SportsData.Api.Config;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Articles.Queries.GetArticleById;

public class GetArticleByIdQueryHandlerTests : ApiTestBase<GetArticleByIdQueryHandler>
{
    private void SetupApiConfig()
    {
        var apiConfig = new ApiConfig { BaseUrl = "http://localhost:5262", UserIdSystem = Guid.NewGuid() };
        Mocker.GetMock<IOptions<ApiConfig>>()
            .Setup(x => x.Value)
            .Returns(apiConfig);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenArticleDoesNotExist()
    {
        // Arrange
        SetupApiConfig();
        var articleId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<GetArticleByIdQueryHandler>();
        var query = new GetArticleByIdQuery { ArticleId = articleId };

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
