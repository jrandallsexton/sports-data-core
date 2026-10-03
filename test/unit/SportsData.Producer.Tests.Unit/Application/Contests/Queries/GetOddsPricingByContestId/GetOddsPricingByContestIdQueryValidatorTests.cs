using FluentAssertions;

using SportsData.Producer.Application.Contests.Queries.GetOddsPricingByContestId;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetOddsPricingByContestId;

public class GetOddsPricingByContestIdQueryValidatorTests
{
    private readonly GetOddsPricingByContestIdQueryValidator _sut = new();

    [Fact]
    public void AContestId_IsValid()
    {
        _sut.Validate(new GetOddsPricingByContestIdQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyGuid_IsRejected()
    {
        _sut.Validate(new GetOddsPricingByContestIdQuery(Guid.Empty)).IsValid.Should().BeFalse();
    }
}
