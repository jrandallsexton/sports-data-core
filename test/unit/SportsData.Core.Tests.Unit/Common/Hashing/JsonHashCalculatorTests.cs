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
}