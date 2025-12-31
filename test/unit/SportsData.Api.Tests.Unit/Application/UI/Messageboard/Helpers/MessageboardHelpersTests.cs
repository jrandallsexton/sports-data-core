using FluentAssertions;

using SportsData.Api.Application.UI.Messageboard.Helpers;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard.Helpers;

public class MessageboardHelpersTests
{
    public class AdjustReactionCountsTests
    {
        [Theory]
        [InlineData(ReactionType.Like)]
        [InlineData(ReactionType.Laugh)]
        [InlineData(ReactionType.Surprise)]
        public void AdjustReactionCounts_ShouldIncrementLikeCount_ForPositiveReactions(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 2
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: false);

            // Assert
            post.LikeCount.Should().Be(6);
            post.DislikeCount.Should().Be(2);
        }

        [Theory]
        [InlineData(ReactionType.Dislike)]
        [InlineData(ReactionType.Sad)]
        [InlineData(ReactionType.Angry)]
        public void AdjustReactionCounts_ShouldIncrementDislikeCount_ForNegativeReactions(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 2
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: false);

            // Assert
            post.LikeCount.Should().Be(5);
            post.DislikeCount.Should().Be(3);
        }

        [Theory]
        [InlineData(ReactionType.Like)]
        [InlineData(ReactionType.Laugh)]
        [InlineData(ReactionType.Surprise)]
        public void AdjustReactionCounts_ShouldDecrementLikeCount_ForPositiveReactionsWhenDecrementing(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 2
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: true);

            // Assert
            post.LikeCount.Should().Be(4);
            post.DislikeCount.Should().Be(2);
        }

        [Theory]
        [InlineData(ReactionType.Dislike)]
        [InlineData(ReactionType.Sad)]
        [InlineData(ReactionType.Angry)]
        public void AdjustReactionCounts_ShouldDecrementDislikeCount_ForNegativeReactionsWhenDecrementing(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 2
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: true);

            // Assert
            post.LikeCount.Should().Be(5);
            post.DislikeCount.Should().Be(1);
        }

        [Theory]
        [InlineData(ReactionType.Like)]
        [InlineData(ReactionType.Laugh)]
        [InlineData(ReactionType.Surprise)]
        public void AdjustReactionCounts_ShouldNotGoBelowZero_WhenDecrementingLikeCount(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 0,
                DislikeCount = 5
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: true);

            // Assert
            post.LikeCount.Should().Be(0, "LikeCount should be clamped to 0");
            post.DislikeCount.Should().Be(5);
        }

        [Theory]
        [InlineData(ReactionType.Dislike)]
        [InlineData(ReactionType.Sad)]
        [InlineData(ReactionType.Angry)]
        public void AdjustReactionCounts_ShouldNotGoBelowZero_WhenDecrementingDislikeCount(ReactionType type)
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 0
            };

            // Act
            MessageboardHelpers.AdjustReactionCounts(post, type, decrement: true);

            // Assert
            post.LikeCount.Should().Be(5);
            post.DislikeCount.Should().Be(0, "DislikeCount should be clamped to 0");
        }

        [Fact]
        public void AdjustReactionCounts_ShouldThrowArgumentOutOfRangeException_ForInvalidReactionType()
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 5,
                DislikeCount = 2
            };

            var invalidType = (ReactionType)999;

            // Act & Assert
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                MessageboardHelpers.AdjustReactionCounts(post, invalidType, decrement: false));

            exception.ParamName.Should().Be("type");
            exception.Message.Should().Contain("Unknown reaction type");
        }

        [Fact]
        public void AdjustReactionCounts_ShouldHandleMultipleDecrements_WithoutGoingNegative()
        {
            // Arrange
            var post = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = Guid.NewGuid(),
                Content = "Test",
                CreatedBy = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                Depth = 0,
                Path = "0001",
                LikeCount = 1,
                DislikeCount = 1
            };

            // Act - Decrement multiple times
            MessageboardHelpers.AdjustReactionCounts(post, ReactionType.Like, decrement: true);
            MessageboardHelpers.AdjustReactionCounts(post, ReactionType.Like, decrement: true);
            MessageboardHelpers.AdjustReactionCounts(post, ReactionType.Like, decrement: true);

            MessageboardHelpers.AdjustReactionCounts(post, ReactionType.Dislike, decrement: true);
            MessageboardHelpers.AdjustReactionCounts(post, ReactionType.Dislike, decrement: true);

            // Assert
            post.LikeCount.Should().Be(0, "should be clamped to 0 after multiple decrements");
            post.DislikeCount.Should().Be(0, "should be clamped to 0 after multiple decrements");
        }
    }

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
