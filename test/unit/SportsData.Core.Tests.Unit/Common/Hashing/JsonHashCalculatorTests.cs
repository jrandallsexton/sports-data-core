using SportsData.Core.Common.Hashing;

using Xunit;

namespace SportsData.Core.Tests.Unit.Common.Hashing;

public class JsonHashCalculatorTests
{
    private readonly IJsonHashCalculator _calculator = new JsonHashCalculator();

    [Fact]
    public void Hash_ShouldBeConsistent_ForEquivalentJson()
    {
        var json1 = "{ \"name\": \"LSU\", \"id\": 99 }";
        var json2 = "{ \"id\": 99, \"name\": \"LSU\" }";

        var hash1 = _calculator.NormalizeAndHash(json1);
        var hash2 = _calculator.NormalizeAndHash(json2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_ShouldChange_IfJsonChanges()
    {
        var json1 = "{ \"name\": \"LSU\", \"id\": 99 }";
        var json2 = "{ \"name\": \"LSU\", \"id\": 100 }";

        var hash1 = _calculator.NormalizeAndHash(json1);
        var hash2 = _calculator.NormalizeAndHash(json2);

        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n  ")]
    [InlineData("\uFEFF")]
    [InlineData("\uFEFF   ")]
    public void Hash_ShouldReturnStableHash_ForEmptyOrWhitespaceInput(string input)
    {
        var hash = _calculator.NormalizeAndHash(input);
        Assert.False(string.IsNullOrWhiteSpace(hash));
        // Optionally assert the exact SHA256 of empty string:
        Assert.Equal("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855", hash);
    }
}
