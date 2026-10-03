using Moq;

using SportsData.Api.Application.Prompts.Commands.CreatePrompt;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Prompts.Commands.CreatePrompt
{
    public class CreatePromptCommandHandlerTests : ApiTestBase<CreatePromptCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        private CreatePromptCommandHandler BuildSut()
        {
            Mocker.GetMock<IDateTimeProvider>()
                .Setup(x => x.UtcNow())
                .Returns(Now);
            return Mocker.CreateInstance<CreatePromptCommandHandler>();
        }

        [Fact]
        public async Task Create_SetsDefault_AndFlipsPreviousDefaultInSlot()
        {
            // Arrange — an existing default in the same (Sport=null, WithStats) slot
            await DataContext.Prompts.AddAsync(new Prompt
            {
                Id = Guid.NewGuid(),
                Name = "prediction-insights-v1",
                Sport = null,
                WithStats = false,
                IsDefault = true,
                Text = "OLD"
            });
            // Different slot — must be untouched
            var withStatsDefault = new Prompt
            {
                Id = Guid.NewGuid(),
                Name = "with-stats",
                Sport = null,
                WithStats = true,
                IsDefault = true,
                Text = "STATS"
            };
            await DataContext.Prompts.AddAsync(withStatsDefault);
            await DataContext.SaveChangesAsync();

            var sut = BuildSut();

            // Act
            var result = await sut.ExecuteAsync(new CreatePromptCommand
            {
                Name = "prediction-insights-v2",
                Sport = null,
                WithStats = false,
                IsDefault = true,
                Text = "NEW\r\nLINE"
            }, CancellationToken.None);

            // Assert
            Assert.True(result.IsSuccess);

            var slotDefaults = DataContext.Prompts
                .Where(p => p.WithStats == false && p.IsDefault).ToList();
            var newDefault = Assert.Single(slotDefaults);
            Assert.Equal("prediction-insights-v2", newDefault.Name);
            Assert.Equal("NEW\nLINE", newDefault.Text); // CRLF normalized to LF

            Assert.True(DataContext.Prompts.Single(p => p.Id == withStatsDefault.Id).IsDefault);
        }

        [Fact]
        public async Task Create_RejectsDuplicateName()
        {
            await DataContext.Prompts.AddAsync(new Prompt
            {
                Id = Guid.NewGuid(),
                Name = "prediction-insights-v1",
                WithStats = false,
                Text = "EXISTING"
            });
            await DataContext.SaveChangesAsync();

            var sut = BuildSut();

            var result = await sut.ExecuteAsync(new CreatePromptCommand
            {
                Name = "prediction-insights-v1",
                WithStats = true,
                Text = "OTHER"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Single(DataContext.Prompts);
        }

        [Fact]
        public async Task Create_RejectsNameLongerThanThePromptVersionColumn()
        {
            // The name is written to MatchupPreviewPrompt.PromptVersion (50) on
            // every capture; a longer name must be refused at creation, not
            // accepted and then fail every generation.
            var sut = BuildSut();

            var result = await sut.ExecuteAsync(new CreatePromptCommand
            {
                Name = new string('n', MatchupPreviewPrompt.PromptVersionMaxLength + 1),
                WithStats = true,
                Text = "TEXT"
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ResultStatus.Validation, result.Status);
            Assert.Empty(DataContext.Prompts);
        }

        [Fact]
        public async Task Create_AcceptsNameAtTheLimit_MeasuredAfterTrim()
        {
            // Exactly at the limit once trimmed (the handler stores Name.Trim()),
            // with surrounding whitespace that must not count against it.
            var sut = BuildSut();
            var name = new string('n', MatchupPreviewPrompt.PromptVersionMaxLength);

            var result = await sut.ExecuteAsync(new CreatePromptCommand
            {
                Name = $"  {name}  ",
                WithStats = true,
                Text = "TEXT"
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(name, Assert.Single(DataContext.Prompts).Name);
        }
    }
}
