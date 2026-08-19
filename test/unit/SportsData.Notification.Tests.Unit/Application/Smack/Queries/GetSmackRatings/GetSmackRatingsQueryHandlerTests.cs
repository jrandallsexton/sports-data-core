using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Commands.RateSmackPreview;
using SportsData.Notification.Application.Smack.Queries.GetSmackRatings;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Queries.GetSmackRatings;

public class GetSmackRatingsQueryHandlerTests : SmackTestBase<GetSmackRatingsQueryHandler>
{
    private GetSmackRatingsQueryHandler Sut() => Mocker.CreateInstance<GetSmackRatingsQueryHandler>();

    [Fact]
    public async Task GetRatings_ReturnsLeagueRowsOnly_AndRequiresLeagueId()
    {
        var leagueId = Guid.NewGuid();

        var missing = await Sut().ExecuteAsync(Guid.Empty);
        missing.Should().BeOfType<Failure<List<SmackRatingDto>>>();
        missing.Status.Should().Be(ResultStatus.BadRequest);

        // Seed through the rating command so the query reads exactly what the
        // write path persists.
        var rate = Mocker.CreateInstance<RateSmackPreviewCommandHandler>();
        await rate.ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            LeagueId = leagueId,
            Voice = "Smack",
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = "in-league line",
            Stars = 3
        });
        await rate.ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            LeagueId = Guid.NewGuid(), // different league — must not return
            Voice = "Smack",
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = "other league line",
            Stars = 1
        });

        var result = await Sut().ExecuteAsync(leagueId);

        result.IsSuccess.Should().BeTrue();
        var rows = result.Value;
        rows.Should().HaveCount(1);
        rows[0].RenderedText.Should().Be("in-league line");
        rows[0].Stars.Should().Be(3);
        rows[0].Voice.Should().Be("Smack");
    }
}
