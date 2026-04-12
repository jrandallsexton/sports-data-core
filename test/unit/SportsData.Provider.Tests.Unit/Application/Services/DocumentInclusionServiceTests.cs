#nullable enable

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Provider.Application.Services;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Services
{
    public class DocumentInclusionServiceTests
    {
        private readonly Mock<ILogger<DocumentInclusionService>> _loggerMock;
        private readonly DocumentInclusionService _sut;

        public DocumentInclusionServiceTests()
        {
            _loggerMock = new Mock<ILogger<DocumentInclusionService>>();
            _sut = new DocumentInclusionService(_loggerMock.Object);
        }

        #region GetIncludableJson Tests

        [Fact]
        public void GetIncludableJson_WithSmallValidDocument_ReturnsJson()
        {
            // Arrange
            var smallJson = new string('x', 1000); // 1 KB

            // Act
            var result = _sut.GetIncludableJson(smallJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(smallJson, result);
        }

        [Fact]
        public void GetIncludableJson_WithLargeDocument_ReturnsNull()
        {
            // Arrange - Create document larger than 200KB limit
            var largeJson = new string('x', 300_000); // 300 KB

            // Act
            var result = _sut.GetIncludableJson(largeJson);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetIncludableJson_WithDocumentAtExactLimit_ReturnsJson()
        {
            // Arrange - Create document exactly at 200KB limit
            var json = new string('x', _sut.MaxInlineJsonBytes);

            // Act
            var result = _sut.GetIncludableJson(json);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(json, result);
        }

        [Fact]
        public void GetIncludableJson_WithDocumentOneByteLarger_ReturnsNull()
        {
            // Arrange - Create document one byte over limit
            var json = new string('x', _sut.MaxInlineJsonBytes + 1);

            // Act
            var result = _sut.GetIncludableJson(json);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetIncludableJson_WithNullInput_ReturnsNull()
        {
            // Act
            var result = _sut.GetIncludableJson(null!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetIncludableJson_WithEmptyString_ReturnsNull()
        {
            // Act
            var result = _sut.GetIncludableJson(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetIncludableJson_LogsWarningWhenExceedingLimit()
        {
            // Arrange - Create large document
            var largeJson = new string('x', 300_000);

            // Act
            _sut.GetIncludableJson(largeJson);

            // Assert - Verify warning was logged
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("exceeds") && v.ToString()!.Contains("KB limit")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public void GetIncludableJson_PreservesHtmlEntitiesInJson()
        {
            // Arrange - JSON with HTML entities in string values (from ESPN play descriptions)
            // These must NOT be decoded, as that would break the JSON structure
            var jsonWithEntities = "{\"text\":\"Pass complete on a &quot;Skinny Post&quot;\",\"id\":123}";

            // Act
            var result = _sut.GetIncludableJson(jsonWithEntities);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(jsonWithEntities, result);
            Assert.Contains("&quot;", result);
        }

        #endregion

        #region ExceedsSizeLimit Tests

        [Fact]
        public void ExceedsSizeLimit_WithSmallDocument_ReturnsFalse()
        {
            // Arrange
            var smallJson = new string('x', 1000);

            // Act
            var result = _sut.ExceedsSizeLimit(smallJson);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ExceedsSizeLimit_WithLargeDocument_ReturnsTrue()
        {
            // Arrange
            var largeJson = new string('x', 300_000);

            // Act
            var result = _sut.ExceedsSizeLimit(largeJson);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ExceedsSizeLimit_WithDocumentAtExactLimit_ReturnsFalse()
        {
            // Arrange
            var json = new string('x', _sut.MaxInlineJsonBytes);

            // Act
            var result = _sut.ExceedsSizeLimit(json);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ExceedsSizeLimit_WithDocumentOneByteLarger_ReturnsTrue()
        {
            // Arrange
            var json = new string('x', _sut.MaxInlineJsonBytes + 1);

            // Act
            var result = _sut.ExceedsSizeLimit(json);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ExceedsSizeLimit_WithNullInput_ReturnsFalse()
        {
            // Act
            var result = _sut.ExceedsSizeLimit(null!);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ExceedsSizeLimit_WithEmptyString_ReturnsFalse()
        {
            // Act
            var result = _sut.ExceedsSizeLimit(string.Empty);

            // Assert
            Assert.False(result);
        }

        #endregion

        #region GetDocumentSize Tests

        [Fact]
        public void GetDocumentSize_WithValidJson_ReturnsCorrectSize()
        {
            // Arrange
            var json = new string('x', 1000);

            // Act
            var size = _sut.GetDocumentSize(json);

            // Assert
            Assert.Equal(1000, size);
        }

        [Fact]
        public void GetDocumentSize_WithNullInput_ReturnsZero()
        {
            // Act
            var size = _sut.GetDocumentSize(null!);

            // Assert
            Assert.Equal(0, size);
        }

        [Fact]
        public void GetDocumentSize_WithEmptyString_ReturnsZero()
        {
            // Act
            var size = _sut.GetDocumentSize(string.Empty);

            // Assert
            Assert.Equal(0, size);
        }

        #endregion

        #region MaxInlineJsonBytes Tests

        [Fact]
        public void MaxInlineJsonBytes_Returns200KB()
        {
            // Act
            var maxBytes = _sut.MaxInlineJsonBytes;

            // Assert
            Assert.Equal(204_800, maxBytes); // 200 KB
        }

        #endregion

        #region Integration Scenarios

        [Fact]
        public void Scenario_SmallDocument_Includes()
        {
            // Arrange
            var validJson = "{\"id\":\"333\",\"name\":\"LSU Tigers\"}";

            // Act
            var result = _sut.GetIncludableJson(validJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validJson, result);
        }

        [Fact]
        public void Scenario_LargeDocument_Excludes()
        {
            // Arrange - Create large document that exceeds limit
            var largeJson = new string('x', 300_000);

            // Act
            var result = _sut.GetIncludableJson(largeJson);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Scenario_EspnPlayWithHtmlEntities_PreservesIntact()
        {
            // Arrange - Real ESPN play description with HTML-encoded quotes
            var espnPlayJson = "{\"$ref\":\"http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/events/301031006/competitions/301031006/plays/3010310062184\",\"text\":\"D.Garrard pass short middle to M.Sims-Walker to JAX 48 for 29 yards (G.Sensabaugh). Pass complete on a &quot;Skinny Post&quot;\",\"type\":{\"id\":\"51\",\"text\":\"Pass\"}}";

            // Act
            var result = _sut.GetIncludableJson(espnPlayJson);

            // Assert - HTML entities must be preserved, not decoded
            Assert.NotNull(result);
            Assert.Equal(espnPlayJson, result);
            Assert.Contains("&quot;Skinny Post&quot;", result);
        }

        #endregion
    }
}
