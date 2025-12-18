using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    [Collection("Sequential")]
    public class EventCompetitionDocumentProcessorTests :
        ProducerTestBase<EventCompetitionDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task CanDeserializeCompetitionBroadcasts()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionBroadcasts.json");

            // act
            var broadcastsDto = documentJson.FromJson<EspnEventCompetitionBroadcastDto>();

            // assert
            broadcastsDto.Should().NotBeNull();
            broadcastsDto.Items.Should().NotBeNull();
            broadcastsDto.Items.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task CanDeserializeCompetition()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            // act
            var dto = documentJson.FromJson<EspnEventCompetitionDto>();

            // assert
            dto.Should().NotBeNull();
            dto!.Ref.Should().NotBeNull();
            dto.Competitors.Should().NotBeEmpty();
            dto.Competitors.Should().HaveCount(2);
        }

        [Fact]
        public async Task WhenEntityDoesNotExist_IsAdded()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var bus = Mocker.GetMock<IEventBus>();

            var dto = documentJson.FromJson<EspnEventCompetitionDto>();

            Guid homeId = Guid.Empty;
            Guid awayId = Guid.Empty;

            foreach (var competitor in dto.Competitors)
            {
                var identity = generator.Generate(competitor.Team.Ref);

                if (competitor.HomeAway == "home")
                {
                    homeId = identity.CanonicalId;
                }
                else
                {
                    awayId = identity.CanonicalId;
                }

                var franchiseSeason = new FranchiseSeason
                {
                    Id = Guid.NewGuid(),
                    Abbreviation = "Test",
                    DisplayName = "Test Franchise Season",
                    DisplayNameShort = "Test FS",
                    Slug = identity.CanonicalId.ToString(),
                    Location = "Test Location",
                    Name = "Test Franchise Season",
                    ColorCodeHex = "#FFFFFF",
                    ColorCodeAltHex = "#000000",
                    IsActive = true,
                    SeasonYear = 2024,
                    FranchiseId = Guid.NewGuid(),
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid(),
                    ExternalIds = new List<FranchiseSeasonExternalId>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = identity.CleanUrl,
                            SourceUrlHash = identity.UrlHash,
                            Value = identity.UrlHash
                        }
                    }
                };

                await base.FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            }
            await base.FootballDataContext.SaveChangesAsync();

            // Create the parent contest entity before processing the competition document
            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ShortName = "Test",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                StartDateUtc = DateTime.UtcNow,
                HomeTeamFranchiseSeasonId = homeId,
                AwayTeamFranchiseSeasonId = awayId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.Contests.AddAsync(contest);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en".UrlHash())
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var competition = await base.FootballDataContext.Competitions
                .Include(c => c.ExternalIds)
                .FirstOrDefaultAsync(c => c.ContestId == contest.Id);

            competition.Should().NotBeNull();
            competition!.ContestId.Should().Be(contest.Id);
            competition.ExternalIds.Should().NotBeEmpty();

            // Verify child documents were requested - the exact types depend on what's in the test JSON
            // Just verify that at least some child documents were processed
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionCompetitor), It.IsAny<CancellationToken>()), Times.Exactly(2));
            bus.Verify(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()), Times.AtLeast(5));
        }

        [Fact]
        public async Task WhenEntityExists_IsUpdated()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var bus = Mocker.GetMock<IEventBus>();

            var dto = documentJson.FromJson<EspnEventCompetitionDto>();
            var competitionIdentity = generator.Generate(dto.Ref);

            Guid homeId = Guid.Empty;
            Guid awayId = Guid.Empty;

            foreach (var competitor in dto.Competitors)
            {
                var identity = generator.Generate(competitor.Team.Ref);

                if (competitor.HomeAway == "home")
                {
                    homeId = identity.CanonicalId;
                }
                else
                {
                    awayId = identity.CanonicalId;
                }

                var franchiseSeason = new FranchiseSeason
                {
                    Id = Guid.NewGuid(),
                    Abbreviation = "Test",
                    DisplayName = "Test Franchise Season",
                    DisplayNameShort = "Test FS",
                    Slug = identity.CanonicalId.ToString(),
                    Location = "Test Location",
                    Name = "Test Franchise Season",
                    ColorCodeHex = "#FFFFFF",
                    ColorCodeAltHex = "#000000",
                    IsActive = true,
                    SeasonYear = 2024,
                    FranchiseId = Guid.NewGuid(),
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid(),
                    ExternalIds = new List<FranchiseSeasonExternalId>
                    {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = identity.CleanUrl,
                            SourceUrlHash = identity.UrlHash,
                            Value = identity.UrlHash
                        }
                    }
                };

                await base.FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            }
            await base.FootballDataContext.SaveChangesAsync();

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ShortName = "Test",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                StartDateUtc = DateTime.UtcNow,
                HomeTeamFranchiseSeasonId = homeId,
                AwayTeamFranchiseSeasonId = awayId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.Contests.AddAsync(contest);
            await base.FootballDataContext.SaveChangesAsync();

            // Create existing competition
            var existingCompetition = new Competition
            {
                Id = competitionIdentity.CanonicalId,
                ContestId = contest.Id,
                Date = DateTime.UtcNow.AddDays(-1), // Old date
                Attendance = 50000,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<CompetitionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitionIdentity.CleanUrl,
                        SourceUrlHash = competitionIdentity.UrlHash,
                        Value = competitionIdentity.UrlHash
                    }
                }
            };

            await base.FootballDataContext.Competitions.AddAsync(existingCompetition);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, competitionIdentity.UrlHash)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var competition = await base.FootballDataContext.Competitions
                .Include(c => c.ExternalIds)
                .FirstOrDefaultAsync(c => c.Id == competitionIdentity.CanonicalId);

            competition.Should().NotBeNull();
            
            // Verify update occurred - date should be updated if different in DTO
            competition!.ContestId.Should().Be(contest.Id);

            // Verify child documents were requested (ProcessUpdate should call helper methods)
            // At minimum, verify that some child documents were requested
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionCompetitor), It.IsAny<CancellationToken>()), Times.Exactly(2));
            bus.Verify(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()), Times.AtLeast(5));
        }

        [Fact]
        public async Task WhenDateChanges_PublishesContestStartTimeUpdated()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var bus = Mocker.GetMock<IEventBus>();

            var dto = documentJson.FromJson<EspnEventCompetitionDto>();
            var competitionIdentity = generator.Generate(dto.Ref);

            Guid homeId = Guid.NewGuid();
            Guid awayId = Guid.NewGuid();

            // Create FranchiseSeason entities to satisfy FK constraints
            var homeFranchiseSeason = new FranchiseSeason
            {
                Id = homeId,
                Abbreviation = "HOME",
                DisplayName = "Home Team",
                DisplayNameShort = "Home",
                Slug = "home-team",
                Location = "Home City",
                Name = "Home Team",
                ColorCodeHex = "#FF0000",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var awayFranchiseSeason = new FranchiseSeason
            {
                Id = awayId,
                Abbreviation = "AWAY",
                DisplayName = "Away Team",
                DisplayNameShort = "Away",
                Slug = "away-team",
                Location = "Away City",
                Name = "Away Team",
                ColorCodeHex = "#0000FF",
                ColorCodeAltHex = "#FFFFFF",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.FranchiseSeasons.AddAsync(homeFranchiseSeason);
            await base.FootballDataContext.FranchiseSeasons.AddAsync(awayFranchiseSeason);
            await base.FootballDataContext.SaveChangesAsync();

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ShortName = "Test",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                StartDateUtc = DateTime.UtcNow,
                HomeTeamFranchiseSeasonId = homeId,
                AwayTeamFranchiseSeasonId = awayId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.Contests.AddAsync(contest);

            var oldDate = DateTime.UtcNow.AddDays(-10);
            var existingCompetition = new Competition
            {
                Id = competitionIdentity.CanonicalId,
                ContestId = contest.Id,
                Date = oldDate,
                Attendance = 50000,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<CompetitionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitionIdentity.CleanUrl,
                        SourceUrlHash = competitionIdentity.UrlHash,
                        Value = competitionIdentity.UrlHash
                    }
                },
                Competitors = new List<CompetitionCompetitor>()
            };

            await base.FootballDataContext.Competitions.AddAsync(existingCompetition);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, competitionIdentity.UrlHash)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var competition = await base.FootballDataContext.Competitions
                .FirstOrDefaultAsync(c => c.Id == competitionIdentity.CanonicalId);

            competition.Should().NotBeNull();
            competition!.Date.Should().NotBe(oldDate);

            // Verify ContestStartTimeUpdated event was published
            bus.Verify(x => x.Publish(It.Is<ContestStartTimeUpdated>(e => 
                e.ContestId == contest.Id && 
                e.NewStartTime != oldDate), 
                It.IsAny<CancellationToken>()), 
                Times.Once);
        }

        [Fact]
        public async Task WhenParentIdMissing_LogsErrorAndReturns()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, (string?)null) // Missing ParentId
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var competitions = await base.FootballDataContext.Competitions.ToListAsync();
            competitions.Should().BeEmpty();
        }

        [Fact]
        public async Task WhenContestNotFound_ThrowsException()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var nonExistentContestId = Guid.NewGuid();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, nonExistentContestId.ToString())
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .Create();

            // act & assert
            var act = () => sut.ProcessAsync(command);
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"Contest with ID {nonExistentContestId} not found.");
        }

        [Fact]
        public async Task WhenSeasonYearMissing_LogsErrorAndReturns()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ShortName = "Test",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                StartDateUtc = DateTime.UtcNow,
                HomeTeamFranchiseSeasonId = Guid.NewGuid(),
                AwayTeamFranchiseSeasonId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.Contests.AddAsync(contest);
            await base.FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString())
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, (int?)null) // Missing Season
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var competitions = await base.FootballDataContext.Competitions
                .Where(c => c.ContestId == contest.Id)
                .ToListAsync();
            competitions.Should().BeEmpty();
        }

        [Fact]
        public async Task ProcessUpdate_CallsAllHelperMethods()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var bus = Mocker.GetMock<IEventBus>();

            var dto = documentJson.FromJson<EspnEventCompetitionDto>();
            var competitionIdentity = generator.Generate(dto.Ref);

            Guid homeId = Guid.NewGuid();
            Guid awayId = Guid.NewGuid();

            // Create FranchiseSeason entities to satisfy FK constraints
            var homeFranchiseSeason = new FranchiseSeason
            {
                Id = homeId,
                Abbreviation = "HOME",
                DisplayName = "Home Team",
                DisplayNameShort = "Home",
                Slug = "home-team",
                Location = "Home City",
                Name = "Home Team",
                ColorCodeHex = "#FF0000",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var awayFranchiseSeason = new FranchiseSeason
            {
                Id = awayId,
                Abbreviation = "AWAY",
                DisplayName = "Away Team",
                DisplayNameShort = "Away",
                Slug = "away-team",
                Location = "Away City",
                Name = "Away Team",
                ColorCodeHex = "#0000FF",
                ColorCodeAltHex = "#FFFFFF",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.FranchiseSeasons.AddAsync(homeFranchiseSeason);
            await base.FootballDataContext.FranchiseSeasons.AddAsync(awayFranchiseSeason);
            await base.FootballDataContext.SaveChangesAsync();

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                ShortName = "Test",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                StartDateUtc = DateTime.UtcNow,
                HomeTeamFranchiseSeasonId = homeId,
                AwayTeamFranchiseSeasonId = awayId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await base.FootballDataContext.Contests.AddAsync(contest);

            var existingCompetition = new Competition
            {
                Id = competitionIdentity.CanonicalId,
                ContestId = contest.Id,
                Date = DateTime.UtcNow,
                Attendance = 50000,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<CompetitionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitionIdentity.CleanUrl,
                        SourceUrlHash = competitionIdentity.UrlHash,
                        Value = competitionIdentity.UrlHash
                    }
                },
                Competitors = new List<CompetitionCompetitor>()
            };

            await base.FootballDataContext.Competitions.AddAsync(existingCompetition);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, competitionIdentity.UrlHash)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert - verify all child document types were requested via helper methods
            // The key validation is that ProcessUpdate is using the helper methods (not duplicating code)
            // We verify by checking that DocumentRequested events were published for the major child document types
            
            // Verify the key child documents that should always be present
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionOdds), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionBroadcast), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionPlay), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionLeaders), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionPrediction), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionProbability), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionPowerIndex), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionDrive), It.IsAny<CancellationToken>()), Times.Once);
            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionCompetitor), It.IsAny<CancellationToken>()), Times.Exactly(2));
            
            // This test validates the refactoring was successful - all these verifications passing means
            // ProcessUpdate is correctly calling the helper methods instead of duplicating code
        }
    }
}
