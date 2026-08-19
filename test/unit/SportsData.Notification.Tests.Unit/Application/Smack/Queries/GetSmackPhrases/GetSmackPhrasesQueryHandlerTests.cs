using FluentAssertions;

using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Queries.GetSmackPhrases;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Queries.GetSmackPhrases;

public class GetSmackPhrasesQueryHandlerTests : SmackTestBase<GetSmackPhrasesQueryHandler>
{
    private GetSmackPhrasesQueryHandler Sut() => Mocker.CreateInstance<GetSmackPhrasesQueryHandler>();

    [Fact]
    public async Task GetPhrases_ReturnsFullCatalog_IncludingInactive_WithRowVersion()
    {
        var active = await SeedPhraseAsync(PickSituation.BlowoutLoss, "active line");
        var inactive = await SeedPhraseAsync(PickSituation.UglyWin, "retired line", active: false);

        var result = await Sut().ExecuteAsync();

        result.IsSuccess.Should().BeTrue();
        var phrases = result.Value;
        phrases.Should().HaveCount(2, "the Lab shows inactive rows too");

        var activeDto = phrases.Single(p => p.Id == active.Id);
        activeDto.Text.Should().Be("active line");
        activeDto.Voice.Should().Be(nameof(NotificationVoice.Smack));
        activeDto.Situation.Should().Be(nameof(PickSituation.BlowoutLoss));
        activeDto.IsActive.Should().BeTrue();

        var inactiveDto = phrases.Single(p => p.Id == inactive.Id);
        inactiveDto.IsActive.Should().BeFalse();
        inactiveDto.Text.Should().Be("retired line");
    }
}
