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
    public void Should_Generate_WellFormed_DisplayNames()
    {
        // Generate() draws randomly from 18 adjectives x 43 animals = 774
        // combinations. This test previously asserted 25 draws were all
        // distinct — by the birthday problem that fails ~32% of the time, and
        // uniqueness is not a property the generator promises anyway (it
        // produces DEFAULT display names; duplicates across users are fine,
        // identity lives on Username). Assert the actual contract: every draw
        // is a well-formed adjective_animal pair.
        var names = new List<string>();

        for (int i = 0; i < 25; i++)
        {
            var name = DisplayNameGenerator.Generate();
            _output.WriteLine(name);
            names.Add(name);
        }

        names.Should().AllSatisfy(name =>
        {
            var parts = name.Split('_');
            parts.Should().HaveCount(2);
            parts[0].Should().NotBeNullOrWhiteSpace();
            parts[1].Should().NotBeNullOrWhiteSpace();
        });

        // A constant-output RNG bug would still slip past the shape check;
        // 25 draws over 774 combos being ALL identical is ~(1/774)^24 — a
        // safe non-flaky floor for "randomness is actually happening".
        names.Distinct().Should().HaveCountGreaterThan(1);
    }
}