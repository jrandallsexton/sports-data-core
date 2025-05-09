using SportsData.Producer.Application.Slugs;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Slugs
{
    public class DefaultSlugGeneratorTests : UnitTestBase<DefaultSlugGenerator>
    {
        private readonly DefaultSlugGenerator _slugGenerator = new();

        [Theory]
        [InlineData("MAC", "mac")]
        [InlineData("Big Ten", "big-ten")]
        [InlineData("Big 12", "big-12")]
        [InlineData("CUSA", "cusa")]
        [InlineData("FBS Indep.", "fbs-indep")]
        [InlineData("Mountain West", "mountain-west")]
        [InlineData("ACC", "acc")]
        [InlineData("SEC", "sec")]
        [InlineData("Sun Belt", "sun-belt")]
        [InlineData("American", "american")]
        [InlineData("Pac-12", "pac-12")]
        public void GenerateSlug_ShouldConvertToExpectedFormat(string input, string expected)
        {
            // Act
            var result = _slugGenerator.GenerateSlug(input);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
