using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data.Entities;
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
            SeasonPhase = "Preseason",
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
            },
            SpreadContext = new PreviewSpreadContextDto
            {
                FavoriteTeam = "Arizona Cardinals",
                UnderdogTeam = "Carolina Panthers",
                Magnitude = 6.5,
                SpreadDetails = "ARI -6.5",
                FavoriteWonByMargin = new PreviewMarginFactDto
                {
                    LastGame = new PreviewGameResultDto
                    {
                        GameDate = new DateTime(2025, 11, 2, 18, 0, 0, DateTimeKind.Utc),
                        SeasonYear = 2025,
                        Phase = "Regular Season",
                        HomeTeam = "Arizona Cardinals",
                        AwayTeam = "Tennessee Titans",
                        HomeScore = 31,
                        AwayScore = 17,
                        Winner = "Arizona Cardinals"
                    },
                    OpponentSeasonRecord = "3-14",
                    OpponentPriorSeasonRecord = "5-12",
                    CountLastFiveSeasons = 9,
                    SearchFloorSeason = 2002
                },
                UnderdogLostByMargin = new PreviewMarginFactDto
                {
                    // The headline "never" case: no qualifying game, count 0,
                    // floor intact.
                    LastGame = null,
                    CountLastFiveSeasons = 0,
                    SearchFloorSeason = 2002
                },
                FavoriteAtsAsBigFavorite = new PreviewAtsBucketFactDto
                {
                    Threshold = 3,
                    Games = 12,
                    Covers = 7,
                    DataFloorSeason = 2022
                },
                UnderdogAtsAsBigUnderdog = new PreviewAtsBucketFactDto
                {
                    Threshold = 3,
                    Games = 15,
                    Covers = 9,
                    DataFloorSeason = 2022
                }
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
            // The target game's phase reaches the model (history rows
            // already carried per-game Phase; this closes the asymmetry).
            Assert.Contains("\"SeasonPhase\":\"Preseason\"", capture.PayloadJson);
            Assert.DoesNotContain("AwayRank", capture.PayloadJson);      // null -> omitted
            Assert.DoesNotContain("AwaySpread", capture.PayloadJson);
            Assert.DoesNotContain(_contestId.ToString(), capture.PayloadJson);
            Assert.Contains("AwayPriorSeason", capture.PayloadJson);
            Assert.Contains("\"Wins\":8", capture.PayloadJson);
            Assert.Contains("\"ConferenceWins\":5", capture.PayloadJson);
            Assert.Contains("\"ConferenceLosses\":7", capture.PayloadJson);
            Assert.Contains("\"ConferenceWins\":3", capture.PayloadJson);
            Assert.Contains("\"ConferenceLosses\":9", capture.PayloadJson);

            // Spread-conditioned facts ("The Line") ride in — pre-verified
            // numbers the narrative can cite. The GUID count assertion above
            // already proves the block contributes zero ids; the never-case
            // fact serializes without a LastGame (omit-null) but keeps its
            // count and search floor.
            Assert.Contains("\"SpreadContext\"", capture.PayloadJson);
            Assert.Contains("\"Magnitude\":6.5", capture.PayloadJson);
            Assert.Contains("\"OpponentSeasonRecord\":\"3-14\"", capture.PayloadJson);
            Assert.Contains("\"CountLastFiveSeasons\":9", capture.PayloadJson);
            Assert.Contains("\"CountLastFiveSeasons\":0", capture.PayloadJson);
            Assert.Contains("\"SearchFloorSeason\":2002", capture.PayloadJson);
            Assert.Contains("\"Covers\":7", capture.PayloadJson);
            Assert.Contains("\"DataFloorSeason\":2022", capture.PayloadJson);
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

        // A response whose predictedSpreadWinner contradicts its own predicted
        // scores against a -38.5 home spread (59-25 = margin 34: home does NOT
        // cover; naming home is the contradiction the validator flags).
        private string SpreadResponse(Guid spreadWinner) => $$"""
            {
              "overview": "o",
              "analysis": "a",
              "prediction": "p",
              "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
              "predictedSpreadWinner": "{{spreadWinner}}",
              "overUnderPrediction": 1,
              "awayScore": 25,
              "homeScore": 59
            }
            """;

        [Fact]
        public async Task RealGeneration_RetriesWithFeedback_WhenValidationFails()
        {
            // Arrange — big home spread so the first response can contradict
            // itself; the retry names the correct ATS side and must succeed.
            var matchup = BuildMatchup("STATUS_SCHEDULED");
            matchup.HomeSpread = -38.5;
            matchup.Spread = "ARI -38.5";
            SetupPipeline(matchup);

            Mocker.GetMock<IProvideAiCommunication>()
                .SetupSequence(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(SpreadResponse(_homeFranchiseSeasonId))) // contradiction
                .ReturnsAsync(new Success<string>(SpreadResponse(_awayFranchiseSeasonId))); // corrected

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert — the retry produced a valid preview and the recovery is
            // fully auditable: attempt-1 errors on the capture, iteration
            // count on the preview.
            var preview = Assert.Single(DataContext.MatchupPreviews);
            Assert.Equal(2, preview.IterationsRequired);
            Assert.Equal(_awayFranchiseSeasonId, preview.PredictedSpreadWinner);

            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal(preview.Id, capture.MatchupPreviewId);
            Assert.Contains("inconsistent", capture.ResponseValidationErrors);

            // The second call carried the violation feedback and the original
            // (bad) response back to the model.
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(
                    It.Is<string>(p => p.Contains("It failed validation") && p.Contains("Your previous response")),
                    It.IsAny<CancellationToken>()), Times.Once);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RealGeneration_PersistsAuditableCapture_WhenRetryAlsoFails()
        {
            // Arrange — both attempts contradict themselves: no preview may be
            // written, but the capture must persist with both rounds of errors
            // (previously a validation failure discarded the capture entirely).
            var matchup = BuildMatchup("STATUS_SCHEDULED");
            matchup.HomeSpread = -38.5;
            matchup.Spread = "ARI -38.5";
            SetupPipeline(matchup);

            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(SpreadResponse(_homeFranchiseSeasonId)));

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            // Act
            await sut.Process(command);

            // Assert
            Assert.Empty(DataContext.MatchupPreviews);

            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Null(capture.MatchupPreviewId);
            Assert.NotNull(capture.RawResponse);
            Assert.Contains("Retry:", capture.ResponseValidationErrors);

            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
                PromptId = DefaultPromptId,
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

        [Fact]
        public async Task Process_UnsupportedSport_SkipsBeforeAnyWork()
        {
            // BaseballMlb has no prompts and is the live-pipeline test sport —
            // a throwaway single-day league must never reach the model, or
            // even the Producer round trip (MatchupPreviewPolicy).
            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = Guid.NewGuid(),
                Sport = Sport.BaseballMlb
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();

            await sut.Process(command);

            Mocker.GetMock<IContestClientFactory>()
                .Verify(x => x.Resolve(It.IsAny<Sport>()), Times.Never);
        }

        [Theory]
        [InlineData(Sport.FootballNcaa)]
        [InlineData(Sport.FootballNfl)]
        public void SupportedSports_AreExactlyTheFootballs(Sport sport)
        {
            Assert.True(MatchupPreviewPolicy.SupportsSport(sport));
        }

        [Fact]
        public void BaseballMlb_IsNotSupported()
        {
            Assert.False(MatchupPreviewPolicy.SupportsSport(Sport.BaseballMlb));
        }


        private async Task<Model> SeedLabModelAsync(
            bool modelActive = true,
            bool providerActive = true,
            ModelGateway gateway = ModelGateway.OpenRouter,
            string apiModelId = "openai/gpt-test",
            bool isDefault = false)
        {
            var provider = new ModelProvider
            {
                Id = Guid.NewGuid(),
                Name = $"Provider-{Guid.NewGuid():N}",
                Kind = ModelProviderKind.OpenAi,
                IsActive = providerActive,
                CreatedUtc = DateTime.UtcNow
            };
            var model = new Model
            {
                Id = Guid.NewGuid(),
                ModelProviderId = provider.Id,
                Name = $"Model-{Guid.NewGuid():N}",
                ApiModelId = apiModelId,
                Gateway = gateway,
                IsActive = modelActive,
                IsDefault = isDefault,
                CreatedUtc = DateTime.UtcNow
            };
            await DataContext.ModelProviders.AddAsync(provider);
            await DataContext.Models.AddAsync(model);
            await DataContext.SaveChangesAsync();
            return model;
        }

        [Fact]
        public async Task Generate_ParsesMarkdownFencedJsonResponse()
        {
            // Many models fence JSON out of habit — Claude Haiku did on the
            // lab's first multi-model run and a valid pick was discarded as
            // a parse failure. The fence is cosmetic; the answer counts.
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

            var responseJson = $$"""
                ```json
                {
                  "overview": "o", "analysis": "a", "prediction": "p",
                  "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
                  "predictedSpreadWinner": null,
                  "overUnderPrediction": 2, "awayScore": 17, "homeScore": 27
                }
                ```
                """;
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(responseJson));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("test-model");

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            });

            var preview = Assert.Single(DataContext.MatchupPreviews);
            Assert.Equal(_homeFranchiseSeasonId, preview.PredictedStraightUpWinner);
            Assert.Null(preview.ValidationErrors);
        }

        [Fact]
        public async Task Generate_StampsModelId_WhenDefaultRowMatchesWiredClient()
        {
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

            // The registry's IsDefault row IS the production model selection —
            // when its ApiModelId matches the wired client, the preview gets
            // registry provenance alongside the model string.
            var defaultModel = await SeedLabModelAsync(
                gateway: ModelGateway.None, apiModelId: "test-model", isDefault: true);

            var responseJson = $$"""
                {
                  "overview": "o", "analysis": "a", "prediction": "p",
                  "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
                  "predictedSpreadWinner": null,
                  "overUnderPrediction": 2, "awayScore": 17, "homeScore": 27
                }
                """;
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(responseJson));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("test-model");

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            });

            var preview = Assert.Single(DataContext.MatchupPreviews);
            Assert.Equal(defaultModel.Id, preview.ModelId);
            Assert.Equal("test-model", preview.Model);
            var capture = Assert.Single(DataContext.MatchupPreviewPrompts);
            Assert.Equal(defaultModel.Id, capture.ModelId);
        }

        [Fact]
        public async Task Generate_StampsNoModelId_WhenDefaultRowMismatchesWiredClient()
        {
            SetupPipeline(BuildMatchup("STATUS_SCHEDULED"));

            // Flag/config drift: a null stamp beats a false one.
            await SeedLabModelAsync(
                gateway: ModelGateway.None, apiModelId: "some-other-model", isDefault: true);

            var responseJson = $$"""
                {
                  "overview": "o", "analysis": "a", "prediction": "p",
                  "predictedStraightUpWinner": "{{_homeFranchiseSeasonId}}",
                  "predictedSpreadWinner": null,
                  "overUnderPrediction": 2, "awayScore": 17, "homeScore": 27
                }
                """;
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Success<string>(responseJson));
            Mocker.GetMock<IProvideAiCommunication>()
                .Setup(x => x.GetModelName())
                .Returns("test-model");

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(new GenerateMatchupPreviewsCommand
            {
                ContestId = _contestId,
                Sport = Sport.FootballNfl
            });

            var preview = Assert.Single(DataContext.MatchupPreviews);
            Assert.Null(preview.ModelId);
            Assert.Equal("test-model", preview.Model);
        }

        [Fact]
        public async Task Process_ExperimentWithInactiveModel_Skips()
        {
            // A fan-out enqueued before an admin deactivated the row must not
            // call any model — inactive means inactive, even for in-flight jobs.
            var model = await SeedLabModelAsync(modelActive: false);

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = Guid.NewGuid(),
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment,
                ModelId = model.Id
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(command);

            Mocker.GetMock<IAiModelClientResolver>()
                .Verify(x => x.Resolve(It.IsAny<Model>()), Times.Never);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_ExperimentWithInactiveProvider_Skips()
        {
            // Deactivating a PROVIDER silences its whole fleet at once.
            var model = await SeedLabModelAsync(providerActive: false);

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = Guid.NewGuid(),
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment,
                ModelId = model.Id
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(command);

            Mocker.GetMock<IAiModelClientResolver>()
                .Verify(x => x.Resolve(It.IsAny<Model>()), Times.Never);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_ExperimentWithMissingModel_Skips()
        {
            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = Guid.NewGuid(),
                Sport = Sport.FootballNcaa,
                Mode = PreviewGenerationMode.Experiment,
                ModelId = Guid.NewGuid() // no such Model row
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(command);

            Mocker.GetMock<IAiModelClientResolver>()
                .Verify(x => x.Resolve(It.IsAny<Model>()), Times.Never);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Process_ExperimentWithUnresolvableRoute_SkipsWithoutThrowing()
        {
            // Direct routes have no lab client until panel promotion; that
            // must be a logged skip, never an exception — a throw here
            // would put Hangfire into a pointless retry loop.
            var model = await SeedLabModelAsync(gateway: ModelGateway.None);

            Mocker.GetMock<IAiModelClientResolver>()
                .Setup(x => x.CanResolve(ModelGateway.None, It.IsAny<ModelProviderKind>()))
                .Returns(false);

            var command = new GenerateMatchupPreviewsCommand
            {
                ContestId = Guid.NewGuid(),
                Sport = Sport.FootballNfl,
                Mode = PreviewGenerationMode.Experiment,
                ModelId = model.Id
            };

            var sut = Mocker.CreateInstance<MatchupPreviewProcessor>();
            await sut.Process(command);

            Mocker.GetMock<IAiModelClientResolver>()
                .Verify(x => x.Resolve(It.IsAny<Model>()), Times.Never);
            Mocker.GetMock<IProvideAiCommunication>()
                .Verify(x => x.GetResponseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

    }
}
