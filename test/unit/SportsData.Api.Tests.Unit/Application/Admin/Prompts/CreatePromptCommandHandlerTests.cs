using Moq;

using SportsData.Api.Application.Admin.Prompts;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Blobs;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Prompts
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
        public async Task ImportFromBlob_CreatesPrompt_WithBlobText()
        {
            Mocker.GetMock<IProvideBlobStorage>()
                .Setup(x => x.GetFileContentsAsync("prompts", "prediction-insights-v1.txt", It.IsAny<CancellationToken>()))
                .ReturnsAsync("BLOB TEXT");

            Mocker.Use<ICreatePromptCommandHandler>(BuildSut());
            var importer = Mocker.CreateInstance<ImportPromptFromBlobCommandHandler>();

            var result = await importer.ExecuteAsync(new ImportPromptFromBlobCommand
            {
                BlobName = "prediction-insights-v1", // extension optional
                Sport = null,
                WithStats = false,
                IsDefault = true
            }, CancellationToken.None);

            Assert.True(result.IsSuccess);

            var prompt = Assert.Single(DataContext.Prompts);
            Assert.Equal("prediction-insights-v1", prompt.Name); // legacy PromptVersion value
            Assert.Equal("BLOB TEXT", prompt.Text);
            Assert.True(prompt.IsDefault);
        }
    }
}
