using FluentAssertions;

using SportsData.Api.Application.User;

using Xunit;
using Xunit.Abstractions;

namespace SportsData.Api.Tests.Unit.Application.User;

public class DisplayNameGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public DisplayNameGeneratorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Should_Generate_Unique_DisplayNames()
    {
        var names = new HashSet<string>();

        for (int i = 0; i < 25; i++)
        {
            var name = DisplayNameGenerator.Generate();
            _output.WriteLine(name);
            names.Add(name);
        }

        names.Should().HaveCount(25); // ensure no duplicates in this small sample
    }
}