using Moq;

using SportsData.Api.Application.Prompts.Commands.CreatePrompt;
using SportsData.Api.Application.Prompts.Commands.ImportPromptFromBlob;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Blobs;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Prompts.Commands.ImportPromptFromBlob
{
    public class ImportPromptFromBlobCommandHandlerTests : ApiTestBase<ImportPromptFromBlobCommandHandler>
    {
        private static readonly DateTime Now = new(2026, 8, 8, 12, 0, 0, DateTimeKind.Utc);

        private CreatePromptCommandHandler BuildCreateHandler()
        {
            Mocker.GetMock<IDateTimeProvider>()
                .Setup(x => x.UtcNow())
                .Returns(Now);
            return Mocker.CreateInstance<CreatePromptCommandHandler>();
        }

        [Fact]
        public async Task ImportFromBlob_RejectsBlobNameLongerThanThePromptVersionColumn()
        {
            // Import names the prompt after the blob when no Name is given; it
            // goes through the same validator.
            var longName = new string('b', MatchupPreviewPrompt.PromptVersionMaxLength + 1);
            Mocker.GetMock<IProvideBlobStorage>()
                .Setup(x => x.GetFileContentsAsync("prompts", $"{longName}.txt", It.IsAny<CancellationToken>()))
                .ReturnsAsync("BLOB TEXT");

            Mocker.Use<ICreatePromptCommandHandler>(BuildCreateHandler());
            var importer = Mocker.CreateInstance<ImportPromptFromBlobCommandHandler>();

            var result = await importer.ExecuteAsync(new ImportPromptFromBlobCommand
            {
                BlobName = longName,
                WithStats = false
            }, CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(ResultStatus.Validation, result.Status);
            Assert.Empty(DataContext.Prompts);
        }

        [Fact]
        public async Task ImportFromBlob_CreatesPrompt_WithBlobText()
        {
            Mocker.GetMock<IProvideBlobStorage>()
                .Setup(x => x.GetFileContentsAsync("prompts", "prediction-insights-v1.txt", It.IsAny<CancellationToken>()))
                .ReturnsAsync("BLOB TEXT");

            Mocker.Use<ICreatePromptCommandHandler>(BuildCreateHandler());
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
