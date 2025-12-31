using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Helpers;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Helpers;

public class MessageboardHelpersTests
{
    public class ToFixedBase36Tests
    {
        [Fact]
        public void ToFixedBase36_WithValidInputs_ReturnsCorrectBase36String()
        {
            // Arrange
            int value = 42;
            int width = 4;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be("0016"); // 42 in base-36 is "16"
        }

        [Fact]
        public void ToFixedBase36_WithZeroValue_ReturnsZeroPadded()
        {
            // Arrange
            int value = 0;
            int width = 5;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be("00000");
        }

        [Fact]
        public void ToFixedBase36_WithLargeValue_ReturnsCorrectConversion()
        {
            // Arrange
            int value = 1296; // 10 in base-36 is "ZZ"
            int width = 3;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be("100"); // 1296 in base-36 is "100"
        }

        [Fact]
        public void ToFixedBase36_WithWidthLargerThanNeeded_PadsWithZeros()
        {
            // Arrange
            int value = 35; // 35 in base-36 is "Z"
            int width = 6;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be("00000Z");
        }

        [Fact]
        public void ToFixedBase36_WithWidthEqualToLength_NoPadding()
        {
            // Arrange
            int value = 36; // 36 in base-36 is "10"
            int width = 2;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be("10");
        }

        [Fact]
        public void ToFixedBase36_WithNegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int value = -1;
            int width = 4;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("value");
            exception.Message.Should().Contain("Value must be >= 0");
        }

        [Fact]
        public void ToFixedBase36_WithLargeNegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int value = -100;
            int width = 4;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("value");
            exception.Message.Should().Contain("Value must be >= 0");
        }

        [Fact]
        public void ToFixedBase36_WithZeroWidth_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int value = 42;
            int width = 0;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("width");
            exception.Message.Should().Contain("Width must be > 0");
        }

        [Fact]
        public void ToFixedBase36_WithNegativeWidth_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            int value = 42;
            int width = -5;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("width");
            exception.Message.Should().Contain("Width must be > 0");
        }

        [Fact]
        public void ToFixedBase36_WithBothInvalidInputs_ThrowsForValueFirst()
        {
            // Arrange
            int value = -1;
            int width = -1;

            // Act & Assert
            // Should validate 'value' first based on parameter order
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("value");
        }

        [Theory]
        [InlineData(0, 1, "0")]
        [InlineData(1, 1, "1")]
        [InlineData(9, 1, "9")]
        [InlineData(10, 1, "A")]
        [InlineData(35, 1, "Z")]
        [InlineData(36, 2, "10")]
        [InlineData(1295, 2, "ZZ")]
        public void ToFixedBase36_WithVariousValidInputs_ReturnsExpectedResult(
            int value, 
            int width, 
            string expected)
        {
            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(-1, 4)]
        [InlineData(-10, 4)]
        [InlineData(-100, 4)]
        [InlineData(int.MinValue, 4)]
        public void ToFixedBase36_WithVariousNegativeValues_ThrowsArgumentOutOfRangeException(
            int value, 
            int width)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("value");
        }

        [Theory]
        [InlineData(42, 0)]
        [InlineData(42, -1)]
        [InlineData(42, -10)]
        [InlineData(42, int.MinValue)]
        public void ToFixedBase36_WithVariousInvalidWidths_ThrowsArgumentOutOfRangeException(
            int value, 
            int width)
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.ToFixedBase36(value, width));
            
            exception.ParamName.Should().Be("width");
        }

        [Fact]
        public void ToFixedBase36_WithMaxInt_DoesNotThrow()
        {
            // Arrange
            int value = int.MaxValue;
            int width = 10;

            // Act
            var result = MessageboardHelpers.ToFixedBase36(value, width);

            // Assert
            result.Should().NotBeNullOrEmpty();
            // int.MaxValue in base-36 is "ZIK0ZJ" (6 characters), so with width 10 it should be padded
            result.Length.Should().BeGreaterThanOrEqualTo(6);
        }
    }
}
