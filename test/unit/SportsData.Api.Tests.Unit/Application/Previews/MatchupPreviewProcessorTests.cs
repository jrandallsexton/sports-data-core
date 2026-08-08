using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Prompts;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;
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

        private static readonly Guid DefaultPromptId = Guid.NewGuid();

        private readonly Guid _contestId = Guid.NewGuid();
        private readonly Guid _homeFranchiseSeasonId = Guid.NewGuid();
        private readonly Guid _awayFranchiseSeasonId = Guid.NewGuid();

        private static readonly DateTime TargetStartUtc = new(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);

        private MatchupForPreviewDto BuildMatchup(string status) => new()
        {
            Sport = Sport.FootballNfl,
            SeasonYear = 2025,
            WeekNumber = 2,
            ContestId = _contestId,
            StartDateUtc = TargetStartUtc,
            Status = status,
            StatusDescription = status == "STATUS_FINAL" ? "Final" : "Scheduled",
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

        private static ContestPreviewHistoryDto BuildHistory() => new()
        {
            HeadToHead =
            [
                new PreviewGameResultDto
                {
                    GameDate = new DateTime(2025, 9, 14, 16, 5, 0, DateTimeKind.Utc),
                    SeasonYear = 2025,
                    Phase = "Regular Season",
                    HomeTeam = "Arizona Cardinals",
                    AwayTeam = "Carolina Panthers",
                    HomeScore = 27,
                    AwayScore = 22,
                    Winner = "Arizona Cardinals",
                    SpreadWinner = "Carolina Panthers",
                    Spread = "ARI -6.5",
                    HomeSpread = -6.5,
                    HomeSpreadOpen = -6.5,
                    OverUnder = 45.5,
                    OverUnderOpen = 45.5,
                    OverOdds = -105,
                    UnderOdds = -115,
                    OverUnderResult = "Over"
                }
            ],
            AwayPriorSeasonGames =
            [
                new PreviewGameResultDto
                {
                    GameDate = new DateTime(2026, 1, 10, 21, 30, 0, DateTimeKind.Utc),
                    SeasonYear = 2025,
                    Phase = "Postseason",
                    Note = "NFC Wild Card",
                    HomeTeam = "Carolina Panthers",
                    AwayTeam = "Los Angeles Rams",
                    HomeScore = 31,
                    AwayScore = 34,
                    Winner = "Los Angeles Rams"
                }
            ],
            HomePriorSeasonGames = [],
            AwayPriorSeason = new PreviewPriorSeasonSummaryDto
            {
                SeasonYear = 2025,
                Wins = 8,
                Losses = 10,
                ConferenceWins = 5,
                ConferenceLosses = 7,
                Metrics = new FranchiseSeasonMetricsDto { GamesPlayed = 18, Ypp = 5.4m }
            },
            HomePriorSeason = new PreviewPriorSeasonSummaryDto
            {
                SeasonYear = 2025,
                Wins = 6,
                Losses = 11,
                ConferenceWins = 3,
                ConferenceLosses = 9,
                Metrics = new FranchiseSeasonMetricsDto { GamesPlayed = 17, Ypp = 4.9m }
            }
        };

        private void SetupPipeline(MatchupForPreviewDto matchup, Result<ContestPreviewHistoryDto>? history = null)
        {
            var contestClient = new Mock<IProvideContests>();
            contestClient
                .Setup(x => x.GetMatchupForPreview(_contestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<MatchupForPreviewDto>(matchup));
            contestClient
                .Setup(x => x.GetContestPreviewHistory(_contestId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(history ?? new Success<ContestPreviewHistoryDto>(BuildHistory()));

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

            // The provider echoes the request so tests can assert both the
            // default-variant selection and PromptId overrides.
            Mocker.GetMock<IMatchupPreviewPromptProvider>()
                .Setup(x => x.GetPromptAsync(It.IsAny<PreviewPromptRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PreviewPromptRequest r, CancellationToken _) => new PreviewPrompt(
                    r.PromptId ?? DefaultPromptId,
                    PromptText,
                    r.PromptId is not null
                        ? "override-prompt"
                        : (r.HasStats ? "prediction-insights-with-stats-schedule" : "prediction-insights-v1")));

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

            // Historical blocks flow into the payload, names only. After the
            // hygiene projection the ONLY GUIDs in the whole payload are the
            // two live FranchiseSeasonIds the output contract requires.
            Assert.Contains("HeadToHead", capture.PayloadJson);
            Assert.Contains("NFC Wild Card", capture.PayloadJson);
            Assert.Contains("ARI -6.5", capture.PayloadJson);
            var guidCount = System.Text.RegularExpressions.Regex.Matches(
                capture.PayloadJson,
                "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}").Count;
            Assert.Equal(2, guidCount);

            // Hygiene projection: string enums, omit-null, no derived
            // away-relative spread, no ContestId — and the prior-season
            // summaries ride in.
            Assert.Contains("\"Sport\":\"FootballNfl\"", capture.PayloadJson);
            Assert.DoesNotContain("AwayRank", capture.PayloadJson);      // null -> omitted
            Assert.DoesNotContain("AwaySpread", capture.PayloadJson);
            Assert.DoesNotContain(_contestId.ToString(), capture.PayloadJson);
            Assert.Contains("AwayPriorSeason", capture.PayloadJson);
            Assert.Contains("\"Wins\":8", capture.PayloadJson);
            Assert.Contains("\"ConferenceWins\":5", capture.PayloadJson);
            Assert.Contains("\"ConferenceLosses\":7", capture.PayloadJson);
            Assert.Contains("\"ConferenceWins\":3", capture.PayloadJson);
            Assert.Contains("\"ConferenceLosses\":9", capture.PayloadJson);
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

            // The preview records WHICH Prompt entity generated it — the FK
            // that makes used prompts immutable.
            Assert.Equal(DefaultPromptId, preview.PromptId);
            Assert.Equal(preview.Id, capture.MatchupPreviewId);
            Assert.Equal(PreviewGenerationMode.Generate, capture.Mode);
            Assert.Equal(preview.PromptId, capture.PromptId);
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
        public async Task Capture_OnCompletedContest_ExcludesTargetGameAndMasksStatus()
        {
            // Arrange — the raw CompetitionResults for a completed target
            // contain the TARGET GAME ITSELF (the answer) plus an earlier
            // game. Only the earlier game may reach the payload, and the
            // completed status must read as pre-game.
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            var targetRow = new FranchiseSeasonCompetitionResultDto
            {
                ContestId = _contestId,
                StartDateUtc = TargetStartUtc,
                AwaySlug = "carolina-panthers",
                HomeSlug = "arizona-cardinals",
                AwayShort = "CAR",
                HomeShort = "ARI",
                AwayScore = 33,
                HomeScore = 30
            };
            var earlierRow = new FranchiseSeasonCompetitionResultDto
            {
                ContestId = Guid.NewGuid(),
                StartDateUtc = TargetStartUtc.AddDays(-210),
                AwaySlug = "carolina-panthers",
                HomeSlug = "tampa-bay-buccaneers",
                AwayShort = "CAR",
                HomeShort = "TB",
                AwayScore = 14,
                HomeScore = 16
            };

            var franchiseClient = new Mock<IProvideFranchises>();
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonPreviewStats(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new FranchiseSeasonModelStatsDto { RushingYardsPerGame = 150.0 });
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonMetricsByFranchiseSeasonId(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FranchiseSeasonMetricsDto?)null!);
            franchiseClient
                .Setup(x => x.GetFranchiseSeasonCompetitionResults(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([targetRow, earlierRow]);
            Mocker.GetMock<IFranchiseClientFactory>()
                .Setup(x => x.Resolve(It.IsAny<Sport>()))
                .Returns(franchiseClient.Object);

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Capture
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert — the answer is gone, the earlier game survives, and the
            // status reads pre-game.
            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);

            using var payload = System.Text.Json.JsonDocument.Parse(capture.PayloadJson);

            foreach (var listName in new[] { "AwayCompetitionResults", "HomeCompetitionResults" })
            {
                var results = payload.RootElement.GetProperty(listName);
                Assert.Equal(1, results.GetArrayLength());
                Assert.Equal("tampa-bay-buccaneers", results[0].GetProperty("HomeSlug").GetString());
                Assert.DoesNotContain(_contestId.ToString(), results.GetRawText());
            }

            Assert.Equal("STATUS_SCHEDULED", payload.RootElement.GetProperty("Status").GetString());
            Assert.Equal("Scheduled", payload.RootElement.GetProperty("StatusDescription").GetString());
        }

        [Fact]
        public async Task Capture_AppliesBothOrNothing_ToPriorSeasonMetrics()
        {
            // Arrange — away has prior metrics, home does not: asymmetric
            // analytics would bias the model, so both must be nulled while
            // the records keep flowing.
            var history = BuildHistory();
            history.HomePriorSeason!.Metrics = null;
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"), new Success<ContestPreviewHistoryDto>(history));

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
            using var payload = System.Text.Json.JsonDocument.Parse(capture.PayloadJson);

            var awayPrior = payload.RootElement.GetProperty("AwayPriorSeason");
            Assert.Equal(8, awayPrior.GetProperty("Wins").GetInt32());
            Assert.Equal(5, awayPrior.GetProperty("ConferenceWins").GetInt32());
            Assert.Equal(7, awayPrior.GetProperty("ConferenceLosses").GetInt32());
            Assert.False(awayPrior.TryGetProperty("Metrics", out _)); // nulled -> omitted

            var homePrior = payload.RootElement.GetProperty("HomePriorSeason");
            Assert.Equal(6, homePrior.GetProperty("Wins").GetInt32());
            Assert.Equal(3, homePrior.GetProperty("ConferenceWins").GetInt32());
            Assert.Equal(9, homePrior.GetProperty("ConferenceLosses").GetInt32());
            Assert.False(homePrior.TryGetProperty("Metrics", out _));
        }

        [Fact]
        public async Task Capture_ProceedsWithoutHistory_WhenHistoryFetchFails()
        {
            // Arrange — history endpoint down: preview assembly must degrade
            // gracefully, not fail the job.
            SetupPipeline(
                BuildMatchup("STATUS_SCHEDULED"),
                new Failure<ContestPreviewHistoryDto>(
                    default!,
                    ResultStatus.Error,
                    [new FluentValidation.Results.ValidationFailure("history", "unavailable")]));

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
            Assert.DoesNotContain("NFC Wild Card", capture.PayloadJson);
        }

        [Fact]
        public async Task Experiment_WithPromptId_UsesOverrideAndRecordsIt()
        {
            // Arrange
            SetupPipeline(BuildMatchup("STATUS_FINAL"));

            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>("not json — irrelevant here"));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("experiment-model");

            var overridePromptId = Guid.NewGuid();

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment,
                PromptId = overridePromptId
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert — the override reached the provider, and the capture
            // records BOTH the Prompt entity id and its name for provenance.
            Mocker.GetMock<IMatchupPreviewPromptProvider>()
                .Verify(x => x.GetPromptAsync(
                    It.Is<PreviewPromptRequest>(r => r.PromptId == overridePromptId),
                    It.IsAny<CancellationToken>()), Times.Once);

            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal(overridePromptId, capture.PromptId);
            Assert.Equal("override-prompt", capture.PromptVersion);
        }

        [Fact]
        public async Task Generate_IgnoresPromptId()
        {
            // Arrange — an experiment override must never leak into a real
            // generation; the provider must be asked for the DEFAULT prompt.
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

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
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Generate,
                PromptId = Guid.NewGuid() // rogue override — must be ignored
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Mocker.GetMock<IMatchupPreviewPromptProvider>()
                .Verify(x => x.GetPromptAsync(
                    It.Is<PreviewPromptRequest>(r => r.PromptId == null),
                    It.IsAny<CancellationToken>()), Times.Once);

            var preview = Assert.Single(DataContext.MatchupPreviews);
            Assert.Equal(DefaultPromptId, preview.PromptId);
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
