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

        #region DecodeJson Tests

        [Fact]
        public void DecodeJson_WithHtmlEncodedJson_DecodesCorrectly()
        {
            // Arrange
            var encodedJson = "{&quot;name&quot;:&quot;LSU Tigers&quot;,&quot;id&quot;:&quot;333&quot;}";

            // Act
            var result = _sut.DecodeJson(encodedJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("{\"name\":\"LSU Tigers\",\"id\":\"333\"}", result);
            Assert.DoesNotContain("&quot;", result);
        }

        [Fact]
        public void DecodeJson_WithMultipleHtmlEntities_DecodesAll()
        {
            // Arrange
            var encodedJson = "{&quot;tag&quot;:&quot;&lt;div&gt;A&amp;B&lt;/div&gt;&quot;,&quot;url&quot;:&quot;api&#x2F;data&quot;}";

            // Act
            var result = _sut.DecodeJson(encodedJson);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("\"<div>A&B</div>\"", result);
            Assert.Contains("\"api/data\"", result);
            Assert.DoesNotContain("&quot;", result);
            Assert.DoesNotContain("&lt;", result);
            Assert.DoesNotContain("&gt;", result);
            Assert.DoesNotContain("&amp;", result);
            Assert.DoesNotContain("&#x2F;", result);
        }

        [Fact]
        public void DecodeJson_WithAlreadyValidJson_ReturnsUnchanged()
        {
            // Arrange
            var validJson = "{\"name\":\"LSU Tigers\",\"id\":\"333\"}";

            // Act
            var result = _sut.DecodeJson(validJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validJson, result);
        }

        [Fact]
        public void DecodeJson_WithNullInput_ReturnsNull()
        {
            // Act
            var result = _sut.DecodeJson(null);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DecodeJson_WithEmptyString_ReturnsNull()
        {
            // Act
            var result = _sut.DecodeJson(string.Empty);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void DecodeJson_WithWhitespaceString_ReturnsWhitespace()
        {
            // Arrange
            var whitespace = "   ";

            // Act
            var result = _sut.DecodeJson(whitespace);

            // Assert - Whitespace is valid input, returns as-is
            Assert.Equal(whitespace, result);
        }

        [Fact]
        public void DecodeJson_WithComplexNestedJson_DecodesCorrectly()
        {
            // Arrange
            var encodedJson = "{&quot;data&quot;:{&quot;nested&quot;:{&quot;value&quot;:&quot;test&quot;}}}";

            // Act
            var result = _sut.DecodeJson(encodedJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("{\"data\":{\"nested\":{\"value\":\"test\"}}}", result);
        }

        #endregion

        #region GetIncludableJson Tests

        [Fact]
        public void GetIncludableJson_WithSmallEncodedDocument_ReturnsDecodedJson()
        {
            // Arrange
            var encodedJson = "{&quot;id&quot;:123,&quot;name&quot;:&quot;Test&quot;}";

            // Act
            var result = _sut.GetIncludableJson(encodedJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("{\"id\":123,\"name\":\"Test\"}", result);
            Assert.DoesNotContain("&quot;", result);
        }

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
        public void GetIncludableJson_WithLargeEncodedDocument_ReturnsNull()
        {
            // Arrange - Create large encoded document that's STILL large after decoding
            // Each repetition: {&quot;data&quot;:&quot;xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx&quot;} 
            // becomes: {"data":"xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"} after decoding
            var largeContent = new string('x', 100); // 100 chars of data per object
            var encodedObject = $"{{&quot;data&quot;:&quot;{largeContent}&quot;}}";
            var largeEncodedJson = string.Concat(Enumerable.Repeat(encodedObject, 3000)); // ~300KB after decoding

            // Act
            var result = _sut.GetIncludableJson(largeEncodedJson);

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
            var result = _sut.GetIncludableJson(null);

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
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("exceeds") && v.ToString().Contains("KB limit")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
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
            var result = _sut.ExceedsSizeLimit(null);

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
            var size = _sut.GetDocumentSize(null);

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
        public void Scenario_EncodedSmallDocument_DecodesAndIncludes()
        {
            // Arrange - Real-world scenario: Small encoded JSON from Cosmos
            var cosmosJson = "{&quot;$ref&quot;:&quot;http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/333&quot;,&quot;id&quot;:&quot;333&quot;,&quot;uid&quot;:&quot;s:80~l:23~t:333&quot;,&quot;slug&quot;:&quot;lsu-tigers&quot;,&quot;location&quot;:&quot;LSU&quot;,&quot;name&quot;:&quot;Tigers&quot;,&quot;nickname&quot;:&quot;Tigers&quot;,&quot;abbreviation&quot;:&quot;LSU&quot;}";

            // Act
            var result = _sut.GetIncludableJson(cosmosJson);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("\"$ref\":", result);
            Assert.Contains("\"LSU\"", result);
            Assert.Contains("\"Tigers\"", result);
            Assert.DoesNotContain("&quot;", result);
        }

        [Fact]
        public void Scenario_EncodedLargeDocument_DecodesButExcludesForSize()
        {
            // Arrange - Large encoded document that decodes but still exceeds limit
            var largeEncodedContent = string.Concat(Enumerable.Repeat("{&quot;data&quot;:&quot;x&quot;}", 20_000));

            // Act
            var result = _sut.GetIncludableJson(largeEncodedContent);

            // Assert
            Assert.Null(result); // Too large even after decoding
        }

        [Fact]
        public void Scenario_AlreadyDecodedSmallDocument_ReturnsAsIs()
        {
            // Arrange - Document already in valid JSON format
            var validJson = "{\"id\":\"333\",\"name\":\"LSU Tigers\"}";

            // Act
            var result = _sut.GetIncludableJson(validJson);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(validJson, result);
        }

        #endregion
    }
}
