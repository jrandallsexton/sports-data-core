using FluentAssertions;

using SportsData.Api.Application.UI.Articles.Queries.GetArticleById;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Articles.Queries.GetArticleById;

public class GetArticleByIdQueryHandlerTests : ApiTestBase<GetArticleByIdQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenArticleDoesNotExist()
    {
        // Arrange
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
