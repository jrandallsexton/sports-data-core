using SportsData.Api.Application.Prompts.Commands.SetDefaultPrompt;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Prompts.Commands.SetDefaultPrompt
{
    public class SetDefaultPromptCommandHandlerTests : ApiTestBase<SetDefaultPromptCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task SetDefault_FlipsOnlyItsOwnSlot()
        {
            // Arrange — two slots, each with a default; a challenger in slot 1
            var oldDefault = new Prompt { Id = Guid.NewGuid(), Name = "v1", Sport = Sport.FootballNfl, WithStats = false, IsDefault = true, Text = "OLD" };
            var challenger = new Prompt { Id = Guid.NewGuid(), Name = "v2", Sport = Sport.FootballNfl, WithStats = false, IsDefault = false, Text = "NEW" };
            var otherSlot = new Prompt { Id = Guid.NewGuid(), Name = "any-sport", Sport = null, WithStats = false, IsDefault = true, Text = "ANY" };
            await DataContext.Prompts.AddRangeAsync(oldDefault, challenger, otherSlot);
            await DataContext.SaveChangesAsync();

            Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
            var sut = Mocker.CreateInstance<SetDefaultPromptCommandHandler>();

            // Act
            var result = await sut.ExecuteAsync(challenger.Id, CancellationToken.None);

            // Assert — slot (FootballNfl, false) flipped; any-sport slot untouched
            Assert.True(result.IsSuccess);
            Assert.True(DataContext.Prompts.Single(p => p.Id == challenger.Id).IsDefault);
            Assert.False(DataContext.Prompts.Single(p => p.Id == oldDefault.Id).IsDefault);
            Assert.True(DataContext.Prompts.Single(p => p.Id == otherSlot.Id).IsDefault);
        }
    }
}
