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

        // With 50 adjectives × 100 animals = 5,000 combinations,
        // probability of collision in 25 samples is ~0.06% (effectively impossible)
        for (int i = 0; i < 25; i++)
        {
            var name = DisplayNameGenerator.Generate();
            _output.WriteLine(name);
            names.Add(name);
        }

        names.Should().HaveCount(25, "all generated names should be unique");
    }
}