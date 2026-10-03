using SportsData.Api.Application.Prompts.Commands.UpdatePrompt;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Prompts.Commands.UpdatePrompt
{
    public class UpdatePromptCommandHandlerTests : ApiTestBase<UpdatePromptCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task Update_EditsTextAndDescription_Only()
        {
            var prompt = new Prompt { Id = Guid.NewGuid(), Name = "v1", Sport = Sport.FootballNcaa, WithStats = true, IsDefault = true, Text = "OLD" };
            await DataContext.Prompts.AddAsync(prompt);
            await DataContext.SaveChangesAsync();

            Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
            var sut = Mocker.CreateInstance<UpdatePromptCommandHandler>();

            var result = await sut.ExecuteAsync(new UpdatePromptCommand
            {
                PromptId = prompt.Id,
                Description = "tuned",
                Text = "NEW\r\nTEXT"
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var updated = DataContext.Prompts.Single(p => p.Id == prompt.Id);
            Assert.Equal("NEW\nTEXT", updated.Text); // CRLF normalized
            Assert.Equal("tuned", updated.Description);
            // Identity and slot untouched
            Assert.Equal("v1", updated.Name);
            Assert.Equal(Sport.FootballNcaa, updated.Sport);
            Assert.True(updated.WithStats);
            Assert.True(updated.IsDefault);
        }

        [Fact]
        public async Task Update_Rejected_WhenPromptGeneratedRealPreviews()
        {
            // Arrange — the prompt has produced actual output; its text is
            // provenance and must be immutable.
            var prompt = new Prompt { Id = Guid.NewGuid(), Name = "used-v1", WithStats = true, IsDefault = true, Text = "USED" };
            await DataContext.Prompts.AddAsync(prompt);
            await DataContext.MatchupPreviews.AddAsync(new MatchupPreview
            {
                Id = Guid.NewGuid(),
                ContestId = Guid.NewGuid(),
                PromptId = prompt.Id
            });
            await DataContext.SaveChangesAsync();

            Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
            var sut = Mocker.CreateInstance<UpdatePromptCommandHandler>();

            // Act
            var result = await sut.ExecuteAsync(new UpdatePromptCommand
            {
                PromptId = prompt.Id,
                Text = "CHANGED"
            }, CancellationToken.None);

            // Assert
            Assert.False(result.IsSuccess);
            Assert.Equal("USED", DataContext.Prompts.Single(p => p.Id == prompt.Id).Text);

            // A prompt only used by EXPERIMENTS (capture rows) stays
            // editable — only MatchupPreview freezes it.
            var labOnly = new Prompt { Id = Guid.NewGuid(), Name = "lab-v1", WithStats = true, Text = "LAB" };
            await DataContext.Prompts.AddAsync(labOnly);
            await DataContext.SaveChangesAsync();

            var labResult = await sut.ExecuteAsync(new UpdatePromptCommand
            {
                PromptId = labOnly.Id,
                Text = "TUNED"
            }, CancellationToken.None);

            Assert.True(labResult.IsSuccess);
        }
    }
}
