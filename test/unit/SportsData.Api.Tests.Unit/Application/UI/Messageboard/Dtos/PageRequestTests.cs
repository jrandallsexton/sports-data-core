using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Dtos;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Dtos;

public class PageRequestTests
{
    [Fact]
    public void Create_WithValidLimit_ReturnsPageRequest()
    {
        // Arrange
        const int validLimit = 50;
        const string cursor = "test-cursor";

        // Act
        var result = PageRequest.Create(validLimit, cursor);

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(validLimit);
        result.Cursor.Should().Be(cursor);
    }

    [Fact]
    public void Create_WithDefaultParameters_ReturnsPageRequestWithDefaultLimit()
    {
        // Act
        var result = PageRequest.Create();

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(20);
        result.Cursor.Should().BeNull();
    }

    [Fact]
    public void Create_WithMinimumLimit_ReturnsPageRequest()
    {
        // Arrange
        const int minLimit = 1;

        // Act
        var result = PageRequest.Create(minLimit);

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(minLimit);
    }

    [Fact]
    public void Create_WithMaximumLimit_ReturnsPageRequest()
    {
        // Arrange
        const int maxLimit = 1000;

        // Act
        var result = PageRequest.Create(maxLimit);

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(maxLimit);
    }

    [Fact]
    public void Create_WithLimitBelowMinimum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int invalidLimit = 0;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PageRequest.Create(invalidLimit));
        exception.ParamName.Should().Be("limit");
        exception.Message.Should().Contain("Limit must be between 1 and 1000");
    }

    [Fact]
    public void Create_WithNegativeLimit_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int invalidLimit = -5;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PageRequest.Create(invalidLimit));
        exception.ParamName.Should().Be("limit");
        exception.Message.Should().Contain("Limit must be between 1 and 1000");
    }

    [Fact]
    public void Create_WithLimitAboveMaximum_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        const int invalidLimit = 1001;

        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PageRequest.Create(invalidLimit));
        exception.ParamName.Should().Be("limit");
        exception.Message.Should().Contain("Limit must be between 1 and 1000");
    }

    [Fact]
    public void Create_WithNullCursor_ReturnsPageRequestWithNullCursor()
    {
        // Arrange
        const int validLimit = 25;

        // Act
        var result = PageRequest.Create(validLimit, null);

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(validLimit);
        result.Cursor.Should().BeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void Create_WithVariousValidLimits_ReturnsPageRequest(int limit)
    {
        // Act
        var result = PageRequest.Create(limit);

        // Assert
        result.Should().NotBeNull();
        result.Limit.Should().Be(limit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(1001)]
    [InlineData(5000)]
    public void Create_WithVariousInvalidLimits_ThrowsArgumentOutOfRangeException(int invalidLimit)
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => PageRequest.Create(invalidLimit));
        exception.ParamName.Should().Be("limit");
    }
}
