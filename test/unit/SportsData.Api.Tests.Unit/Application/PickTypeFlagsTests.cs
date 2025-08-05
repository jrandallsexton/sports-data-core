//using FluentAssertions;

//using SportsData.Api.Application;

//using Xunit;

//namespace SportsData.Api.Tests.Unit.Application
//{
//    public class PickTypeFlagsTests
//    {
//        [Fact]
//        public void PickType_ShouldAllowMultipleFlags()
//        {
//            var combined = PickType.AgainstTheSpread | PickType.Confidence;

//            combined.HasFlag(PickType.AgainstTheSpread).Should().BeTrue();
//            combined.HasFlag(PickType.Confidence).Should().BeTrue();
//            combined.HasFlag(PickType.StraightUp).Should().BeFalse();
//        }

//        [Fact]
//        public void PickType_ShouldEqualExactCombination()
//        {
//            var expected = PickType.AgainstTheSpread | PickType.OverUnder;
//            var actual = PickType.AgainstTheSpread | PickType.OverUnder;

//            actual.Should().Be(expected);
//        }

//        [Fact]
//        public void PickType_ToString_ShouldReturnCombinedNames()
//        {
//            var combined = PickType.StraightUp | PickType.Confidence;

//            combined.ToString().Should().Contain(nameof(PickType.StraightUp))
//                .And.Contain(nameof(PickType.Confidence));
//        }
//    }
//}