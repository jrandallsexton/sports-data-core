using Microsoft.Extensions.DependencyInjection;

using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Core.Infrastructure.Clients.AI;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Previews
{
    public class MatchupPreviewProcessorTests : ApiTestBase<MatchupPreviewProcessor>
    {
        private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        private const string PromptText = "PROMPT INSTRUCTIONS";

        private readonly Guid _contestId = Guid.NewGuid();
        private readonly Guid _homeFranchiseSeasonId = Guid.NewGuid();
        private readonly Guid _awayFranchiseSeasonId = Guid.NewGuid();

        private MatchupForPreviewDto BuildMatchup(string status) => new()
        {
            Sport = Sport.FootballNfl,
            SeasonYear = 2025,
            WeekNumber = 2,
            ContestId = _contestId,
            Status = status,
            Venue = "State Farm Stadium",
            VenueCity = "Glendale",
            Home = "Arizona Cardinals",
            HomeSlug = "arizona-cardinals",
            HomeConferenceSlug = "nfc-west",
            HomeFranchiseSeasonId = _homeFranchiseSeasonId,
            Away = "Carolina Panthers",
            AwaySlug = "carolina-panthers",
            AwayConferenceSlug = "nfc-south",
            AwayFranchiseSeasonId = _awayFranchiseSeasonId
        };

        private void SetupPipeline(MatchupForPreviewDto matchup)
        {
            var contestClient = new Mock<IProvideContests>();
            contestClient
                .Setup(x => x.GetMatchupForPreview(_contestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<MatchupForPreviewDto>(matchup));

            Mocker.GetMock<IContestClientFactory>()
                .Setup(x => x.Resolve(It.IsAny<Sport>()))
                .Returns(contestClient.Object);

            var franchiseClient = new Mock<IProvideFranchises>();
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonPreviewStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FranchiseSeasonModelStatsDto { RushingYardsPerGame = 150.0 });
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FranchiseSeasonMetricsDto?)null!);
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonCompetitionResults(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            Mocker.GetMock<IFranchiseClientFactory>()
                .Setup(x => x.Resolve(It.IsAny<Sport>()))
                .Returns(franchiseClient.Object);

            // Real prompt provider backed by a mocked blob store — the
            // provider class is concrete, so we feed it a real DI container.
            var blobStorage = new Mock<IProvideBlobStorage>();
            blobStorage
                .Setup(x => x.GetFileContentsAsync("prompts", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PromptText);

            var services = new ServiceCollection()
                .AddSingleton(blobStorage.Object)
                .BuildServiceProvider();

            Mocker.Use(new MatchupPreviewPromptProvider(services));

            Mocker.GetMock<IDateTimeProvider>()
                .Setup(x => x.UtcNow())
                .Returns(Now);
        }

        [Fact]
        public async Task CaptureOnly_PersistsCapture_WithoutModelCall_OrPreview()
        {
            // Arrange — completed contest: capture mode must still proceed
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Capture
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal(_contestId, capture.ContestId);
            Assert.Equal(Sport.FootballNfl, capture.Sport);
            Assert.Equal(PreviewGenerationMode.Capture, capture.Mode);
            Assert.Null(capture.MatchupPreviewId);
            Assert.Null(capture.Model);
            Assert.Null(capture.RawResponse);
            Assert.Equal("prediction-insights-with-stats-schedule", capture.PromptVersion);
            Assert.Equal(PromptText, capture.PromptText);
            Assert.Contains("arizona-cardinals", capture.PayloadJson);
            Assert.Null(capture.EditorNote);
            Assert.True(capture.CharCount > PromptText.Length);
            Assert.Equal(capture.CharCount / 4, capture.EstTokens);
            Assert.Equal(Now, capture.CreatedUtc);

            Assert.Empty(DataContext.MatchupPreviews);

            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

            Mocker.GetMock<IEventBus>()
                .Verify(x => x.Publish(It.IsAny<PreviewPromptCaptured>(), It.IsAny<CancellationToken>()), Times.Once);
            Mocker.GetMock<IEventBus>()
                .Verify(x => x.Publish(It.IsAny<PreviewGenerated>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RealGeneration_PersistsCapture_LinkedToPreview()
        {
            // Arrange
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

            // No spread on the matchup -> validator treats it as pick'em, so
            // the response must not name a spread winner.
            var responseJson = $$"""
                {
                  "overview": "o",
                  "analysis": "a",
                  "prediction": "p",
                  "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
                  "predictedSpreadWinner": null,
                  "overUnderPrediction": 2,
                  "awayScore": 17,
                  "homeScore": 27
                }
                """;

            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(responseJson));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("test-model");

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var preview = Assert.Single(DataContext.MatchupPreviews);
            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);

            Assert.Equal(preview.Id, capture.MatchupPreviewId);
            Assert.Equal(PreviewGenerationMode.Generate, capture.Mode);
            Assert.Equal(preview.PromptVersion, capture.PromptVersion);
            Assert.Equal("test-model", capture.Model);
            Assert.NotNull(capture.RawResponse);

            // The model received exactly what was captured
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(
                    $"{PromptText}\n\n{capture.PayloadJson}",
                    It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RealGeneration_SkipsCompletedContest_WithoutCapture()
        {
            // Arrange
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Assert.Empty(DataContext.MatchupPreviewPrompts);
            Assert.Empty(DataContext.MatchupPreviews);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CaptureOnly_IncludesEditorNote_FromRejectedPreview()
        {
            // Arrange
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

            await DataContext.MatchupPreviews.AddAsync(new SportsData.Api.Infrastructure.Data.Entities.MatchupPreview
            {
                Id = Guid.NewGuid(),
                ContestId = _contestId,
                RejectedUtc = Now.AddDays(-1),
                RejectionNote = "Too much spread parroting",
                CreatedUtc = Now.AddDays(-1)
            });
            await DataContext.SaveChangesAsync();

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Capture
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal("Too much spread parroting", capture.EditorNote);
        }

        [Fact]
        public async Task Experiment_OnCompletedContest_StoresResponse_WithoutWritingPreview()
        {
            // Arrange — completed contest with a valid model response: the
            // core protection is that NO MatchupPreview row appears, so a
            // prior season's real preview can never be shadowed.
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            var responseJson = $$"""
                {
                  "overview": "o",
                  "analysis": "a",
                  "prediction": "p",
                  "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
                  "predictedSpreadWinner": null,
                  "overUnderPrediction": 2,
                  "awayScore": 17,
                  "homeScore": 27
                }
                """;

            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(responseJson));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("experiment-model");

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Assert.Empty(DataContext.MatchupPreviews);

            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal(PreviewGenerationMode.Experiment, capture.Mode);
            Assert.Null(capture.MatchupPreviewId);
            Assert.Equal("experiment-model", capture.Model);
            Assert.Equal(responseJson, capture.RawResponse);
            Assert.Null(capture.ResponseValidationErrors);

            Mocker.GetMock<IEventBus>()
                .Verify(x => x.Publish(It.IsAny<PreviewPromptCaptured>(), It.IsAny<CancellationToken>()), Times.Once);
            Mocker.GetMock<IEventBus>()
                .Verify(x => x.Publish(It.IsAny<PreviewGenerated>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Experiment_WithMalformedResponse_RecordsProblems()
        {
            // Arrange
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>("this is not json"));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("experiment-model");

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert — the failed response IS the data: persisted, flagged
            Assert.Empty(DataContext.MatchupPreviews);

            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal("this is not json", capture.RawResponse);
            Assert.NotNull(capture.ResponseValidationErrors);
        }
    }
}
