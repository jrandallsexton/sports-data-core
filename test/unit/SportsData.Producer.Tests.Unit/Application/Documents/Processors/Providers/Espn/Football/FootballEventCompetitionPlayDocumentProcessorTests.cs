#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for FootballEventCompetitionPlayDocumentProcessor.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class FootballEventCompetitionPlayDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string PlayUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/plays/401628334123";

    // The two participants in the KickoffReturnOffense fixture (kicker + returner).
    // The play processor now requests sourcing + throws for retry when a
    // participant's athlete/position can't be resolved, so tests expecting the
    // play to persist must seed these deps.
    private const string KickerAthleteRef = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4430026?lang=en";
    private const string ReturnerAthleteRef = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4869748?lang=en";
    private const string KickerPositionRef = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/22?lang=en";
    private const string ReturnerPositionRef = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/1?lang=en";

    // Fixed "now" for every CreatedUtc in this file. Per CLAUDE.md, tests
    // route timestamps through IDateTimeProvider so seeded entities are
    // deterministic. Matches the pattern in MatchupScheduleProcessorTests.
    private static readonly DateTime FixedUtcNow = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public FootballEventCompetitionPlayDocumentProcessorTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedUtcNow);
    }

    private DateTime UtcNow() => Mocker.Get<IDateTimeProvider>().UtcNow();

    /// <summary>
    /// Seed an in-progress FootballCompetitionStatus row for the given
    /// competition. The play processor's base now throws when no status
    /// row exists (treats it as a transient race with the status
    /// processor and lets MassTransit retry); these tests aren't
    /// exercising that path, so they need a status row to proceed.
    /// </summary>
    private async Task SeedInProgressStatusAsync(Guid competitionId)
    {
        await FootballDataContext.Set<FootballCompetitionStatus>().AddAsync(new FootballCompetitionStatus
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            IsCompleted = false,
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seed the AthleteSeason + AthletePosition rows referenced by the two
    /// participants in the KickoffReturnOffense fixture so the play processor's
    /// strict participant resolution finds them instead of requesting sourcing
    /// and throwing for retry. Returns the (kicker, returner) AthleteSeason ids.
    /// </summary>
    private async Task<(Guid KickerSeasonId, Guid ReturnerSeasonId)> SeedKickoffReturnParticipantDepsAsync(
        ExternalRefIdentityGenerator generator)
    {
        Guid SeedAthlete(string refUrl)
        {
            var id = Guid.NewGuid();
            FootballDataContext.AthleteSeasons.Add(new FootballAthleteSeason
            {
                Id = id,
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = UtcNow(),
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<AthleteSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = generator.Generate(refUrl).UrlHash,
                        SourceUrl = new Uri(refUrl).ToCleanUrl(),
                        SourceUrlHash = generator.Generate(refUrl).UrlHash,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            });
            return id;
        }

        void SeedPosition(string refUrl)
        {
            FootballDataContext.AthletePositions.Add(new AthletePosition
            {
                Id = Guid.NewGuid(),
                Name = "Position",
                DisplayName = "Position",
                Abbreviation = "POS",
                CreatedUtc = UtcNow(),
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<AthletePositionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = generator.Generate(refUrl).UrlHash,
                        SourceUrl = new Uri(refUrl).ToCleanUrl(),
                        SourceUrlHash = generator.Generate(refUrl).UrlHash,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            });
        }

        var kicker = SeedAthlete(KickerAthleteRef);
        var returner = SeedAthlete(ReturnerAthleteRef);
        SeedPosition(KickerPositionRef);
        SeedPosition(ReturnerPositionRef);
        await FootballDataContext.SaveChangesAsync();

        return (kicker, returner);
    }

    /// <summary>
    /// Seed AthleteSeason + AthletePosition rows for the given season-scoped
    /// athlete refs and position refs so participant resolution succeeds. Used by
    /// tests processing plays whose participant deps aren't otherwise seeded.
    /// </summary>
    private async Task SeedParticipantDepsAsync(
        ExternalRefIdentityGenerator generator,
        IEnumerable<string> athleteRefs,
        IEnumerable<string> positionRefs)
    {
        foreach (var refUrl in athleteRefs.Distinct())
        {
            FootballDataContext.AthleteSeasons.Add(new FootballAthleteSeason
            {
                Id = Guid.NewGuid(),
                AthleteId = Guid.NewGuid(),
                FranchiseSeasonId = Guid.NewGuid(),
                PositionId = Guid.NewGuid(),
                CreatedUtc = UtcNow(),
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<AthleteSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = generator.Generate(refUrl).UrlHash,
                        SourceUrl = new Uri(refUrl).ToCleanUrl(),
                        SourceUrlHash = generator.Generate(refUrl).UrlHash,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            });
        }

        foreach (var refUrl in positionRefs.Distinct())
        {
            FootballDataContext.AthletePositions.Add(new AthletePosition
            {
                Id = Guid.NewGuid(),
                Name = "Position",
                DisplayName = "Position",
                Abbreviation = "POS",
                CreatedUtc = UtcNow(),
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<AthletePositionExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = generator.Generate(refUrl).UrlHash,
                        SourceUrl = new Uri(refUrl).ToCleanUrl(),
                        SourceUrlHash = generator.Generate(refUrl).UrlHash,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            });
        }

        await FootballDataContext.SaveChangesAsync();
    }

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(PlayUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenJsonIsValid_CanDeserialize()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_Debug.json");
        var play = json.FromJson<EspnFootballEventCompetitionPlayDto>();

        // act


        // assert
        play.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenPlayCollectionIsProvided_ScoreCanBeCalculate()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>();

        // act
        var scoringPlays = plays!.Where(x => x.ScoringPlay).ToList();

        // assert
        scoringPlays.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AsFootballEntity_CapturesScoringTypePointAfterAttemptAndWallclock()
    {
        // Real fixture: a touchdown play carries scoringType + pointAfterAttempt +
        // wallclock — all previously dropped by the mapper.
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>()!;
        var td = plays.First(p => p.ScoringType?.Abbreviation == "TD" && p.PointAfterAttempt != null);

        var entity = td.AsFootballEntity(
            new ExternalRefIdentityGenerator(),
            correlationId: Guid.NewGuid(),
            competitionId: Guid.NewGuid(),
            driveId: null,
            startFranchiseSeasonId: null,
            endFranchiseSeasonId: null);

        entity.ScoringTypeName.Should().Be("touchdown");
        entity.ScoringTypeDisplayName.Should().Be("Touchdown");
        entity.ScoringTypeAbbreviation.Should().Be("TD");
        entity.PointAfterAttemptId.Should().Be(61);
        entity.PointAfterAttemptText.Should().Be("Extra Point Good");
        entity.PointAfterAttemptAbbreviation.Should().Be("Extra Point Good");
        entity.PointAfterAttemptValue.Should().Be(1);
        entity.Wallclock.Should().Be(td.Wallclock);
    }

    [Fact]
    public async Task AsFootballEntity_NonScoringPlay_LeavesScoringFieldsNull()
    {
        // A non-scoring play has no scoringType / pointAfterAttempt.
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnFootballEventCompetitionPlayDto>>()!;
        var nonScoring = plays.First(p => p.ScoringType == null && p.PointAfterAttempt == null);

        var entity = nonScoring.AsFootballEntity(
            new ExternalRefIdentityGenerator(),
            correlationId: Guid.NewGuid(),
            competitionId: Guid.NewGuid(),
            driveId: null,
            startFranchiseSeasonId: null,
            endFranchiseSeasonId: null);

        entity.ScoringTypeName.Should().BeNull();
        entity.ScoringTypeAbbreviation.Should().BeNull();
        entity.PointAfterAttemptId.Should().BeNull();
        entity.PointAfterAttemptValue.Should().BeNull();
    }

    [Fact]
    public async Task WhenProcessingKickoffReturnPlay_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        await SeedInProgressStatusAsync(competitionId);

        // Setup team franchise seasons for both teams
        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99";
        
        // OPTIMIZATION: Direct instantiation
        var startTeam = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM1",
            DisplayName = "Team 1",
            DisplayNameShort = "T1",
            Location = "Location",
            Name = "Team 1",
            Slug = "team-1",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = startTeamId,
                    Provider = SourceDataProvider.Espn,
                    Value = generator.Generate(startTeamUrl).UrlHash,
                    SourceUrl = startTeamUrl,
                    SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(startTeam);
        await FootballDataContext.SaveChangesAsync();

        var returnTeamId = Guid.NewGuid();
        var returnTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var returnTeam = new FranchiseSeason
        {
            Id = returnTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM2",
            DisplayName = "Team 2",
            DisplayNameShort = "T2",
            Location = "Location",
            Name = "Team 2",
            Slug = "team-2",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = returnTeamId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = returnTeamUrl,
                    SourceUrlHash = generator.Generate(returnTeamUrl).UrlHash,
                    Value = generator.Generate(returnTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(returnTeam);
        await FootballDataContext.SaveChangesAsync();

        await SeedKickoffReturnParticipantDepsAsync(generator);

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Type.Should().Be(PlayType.KickoffReturnOffense);
        play.StatYardage.Should().Be(16);
        play.StartYardLine.Should().Be(65);
        play.EndYardLine.Should().Be(23);
        play.StartDown.Should().Be(1);
        play.StartFranchiseSeasonId.Should().Be(returnTeamId);
        play.EndFranchiseSeasonId.Should().Be(startTeamId);
        play.ClockValue.Should().Be(900);
        play.ClockDisplayValue.Should().Be("15:00");
        play.Text.Should().Be("Michael Lantz kickoff for 58 yds , Zavion Thomas return for 16 yds to the LSU 23");
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        await SeedInProgressStatusAsync(competitionId);

        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var franchiseSeason = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // OPTIMIZATION: Direct instantiation
        var externalId = new FranchiseSeasonExternalId
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = startTeamId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = startTeamUrl,
            SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
            Value = generator.Generate(startTeamUrl).UrlHash,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(externalId);

        // Add the FranchiseSeason for dto.Team.Ref
        var teamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en";
        var mainFranchiseSeasonId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var mainFranchiseSeason = new FranchiseSeason
        {
            Id = mainFranchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "MAIN",
            DisplayName = "Main Team",
            DisplayNameShort = "MT",
            Location = "Location",
            Name = "Main Team",
            Slug = "main-team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = mainFranchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = new Uri(teamUrl).ToCleanUrl(),
                    SourceUrlHash = generator.Generate(teamUrl).UrlHash,
                    Value = generator.Generate(teamUrl).UrlHash
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(mainFranchiseSeason);

        await FootballDataContext.SaveChangesAsync();

        await SeedKickoffReturnParticipantDepsAsync(generator);

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Competition.Should().NotBeNull();
        play.Competition!.Id.Should().Be(competitionId);
    }

    [Fact]
    public async Task WhenProcessingPlay_PersistsParticipants_WithResolvedAthleteSeason()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();
        await SeedInProgressStatusAsync(competitionId);

        var (kickerSeasonId, returnerSeasonId) = await SeedKickoffReturnParticipantDepsAsync(generator);

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert - both participants persisted with order/type + resolved athlete/position
        var play = await FootballDataContext.CompetitionPlays
            .FirstAsync(x => x.CompetitionId == competitionId);

        var participants = await FootballDataContext.CompetitionPlayParticipants
            .Where(p => p.CompetitionPlayId == play.Id)
            .OrderBy(p => p.Order)
            .ToListAsync();

        participants.Should().HaveCount(2);

        participants[0].Type.Should().Be("kicker");
        participants[0].Order.Should().Be(1);
        participants[0].AthleteSeasonId.Should().Be(kickerSeasonId);
        participants[0].PositionId.Should().NotBeNull();
        participants[0].StatisticsRef.Should().NotBeNullOrEmpty();

        participants[1].Type.Should().Be("returner");
        participants[1].Order.Should().Be(2);
        participants[1].AthleteSeasonId.Should().Be(returnerSeasonId);
        participants[1].PositionId.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenParticipantAthleteNotSourced_RequestsDependency_AndDoesNotPersistPlay()
    {
        // arrange — competition + status seeded, but NOT the participant athletes
        // or positions, so participant resolution fails.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var bus = Mocker.GetMock<IEventBus>();

        var competitionId = Guid.NewGuid();
        await FootballDataContext.Competitions.AddAsync(new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
        await SeedInProgressStatusAsync(competitionId);

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act — the base swallows ExternalDocumentNotSourcedException and schedules
        // a retry, so ProcessAsync returns without throwing.
        await sut.ProcessAsync(command);

        // assert — the play was NOT persisted (withheld for retry), and sourcing
        // was requested for the unresolved participant dependencies.
        var play = await FootballDataContext.CompetitionPlays
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);
        play.Should().BeNull("the play must be withheld until its participants are sourced");

        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeason),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthletePosition),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenReprocessingExistingPlay_ParticipantNotSourced_LeavesPlayUnmodified_AndRequestsDependency()
    {
        // arrange — an existing play, reprocessed while its participants are NOT
        // sourced. The update path must throw BEFORE mutating the tracked entity,
        // because the base's retry handler calls SaveChangesAsync — otherwise the
        // remapped scalars would be persisted despite "withhold for retry".
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var bus = Mocker.GetMock<IEventBus>();

        var competitionId = Guid.NewGuid();
        await FootballDataContext.Competitions.AddAsync(new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
        await SeedInProgressStatusAsync(competitionId);

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var playRef = json.FromJson<EspnFootballEventCompetitionPlayDto>()!.Ref;
        var canonicalId = generator.Generate(playRef).CanonicalId;
        var urlHash = generator.Generate(playRef).UrlHash;

        // Seed the existing play with stale scalars (and NO participant deps).
        var play = new FootballCompetitionPlay
        {
            Id = canonicalId,
            CompetitionId = competitionId,
            EspnId = "stale-espn-id",
            SequenceNumber = "0",
            Text = "STALE TEXT",
            TypeId = "0",
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        play.ExternalIds.Add(new CompetitionPlayExternalId
        {
            Id = Guid.NewGuid(),
            CompetitionPlayId = canonicalId,
            Provider = SourceDataProvider.Espn,
            Value = urlHash,
            SourceUrl = playRef.AbsoluteUri,
            SourceUrlHash = urlHash,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.CompetitionPlays.AddAsync(play);
        await FootballDataContext.SaveChangesAsync();
        FootballDataContext.ChangeTracker.Clear();

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        // act — update path; participants unsourced → withhold for retry.
        await sut.ProcessAsync(CreateCommand(json, competitionId.ToString()));

        // assert — the existing play is untouched (scalars NOT remapped), no
        // participants were written, and sourcing was requested.
        var reloaded = await FootballDataContext.CompetitionPlays
            .AsNoTracking()
            .FirstAsync(x => x.Id == canonicalId);
        reloaded.Text.Should().Be("STALE TEXT", "the tracked play must not be mutated before the dependency throw");

        (await FootballDataContext.CompetitionPlayParticipants
            .AnyAsync(p => p.CompetitionPlayId == canonicalId))
            .Should().BeFalse();

        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeason),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenEntityExists_FullRemap_RefreshesFields_PreservesIdentityAuditAndExternalIds()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = new FootballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = UtcNow(),
            CreatedUtc = UtcNow(),
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();
        await SeedInProgressStatusAsync(competitionId);

        // Use a SCORING play (touchdown) so the update path must refresh the
        // scoringType / pointAfterAttempt / wallclock columns — seeded null below.
        // Seed an EXISTING play keyed by that play's canonical id with stale Text +
        // null new fields + preserved audit, so the full remap is observable. The
        // processor finds it by canonical id and takes the update path.
        var plays = (await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlays.json"))
            .FromJson<List<EspnFootballEventCompetitionPlayDto>>()!;
        var td = plays.First(p => p.ScoringType?.Abbreviation == "TD" && p.PointAfterAttempt != null);
        var json = td.ToJson();
        var playRef = td.Ref;
        var canonicalId = generator.Generate(playRef).CanonicalId;
        var urlHash = generator.Generate(playRef).UrlHash;
        var originalCreatedUtc = new DateTime(2020, 5, 5, 0, 0, 0, DateTimeKind.Utc);
        var originalCreatedBy = Guid.NewGuid();

        var play = new FootballCompetitionPlay
        {
            Id = canonicalId,
            CompetitionId = competitionId,
            EspnId = "stale-espn-id",
            SequenceNumber = "0",
            Text = "STALE TEXT",
            TypeId = "0",
            CreatedUtc = originalCreatedUtc,
            CreatedBy = originalCreatedBy
        };
        var externalId = new CompetitionPlayExternalId
        {
            Id = Guid.NewGuid(),
            CompetitionPlayId = canonicalId,
            Provider = SourceDataProvider.Espn,
            Value = urlHash,
            SourceUrl = playRef.AbsoluteUri,
            SourceUrlHash = urlHash,
            CreatedBy = Guid.NewGuid()
        };
        play.ExternalIds.Add(externalId);
        await FootballDataContext.CompetitionPlays.AddAsync(play);
        await FootballDataContext.SaveChangesAsync();
        var originalExternalIdRowId = externalId.Id;

        // The TD play carries participants; seed their athlete/position deps so the
        // update path resolves them and performs the full remap (rather than
        // withholding for retry).
        await SeedParticipantDepsAsync(
            generator,
            athleteRefs: new[]
            {
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4429059?lang=en",
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4430026?lang=en"
            },
            positionRefs: new[]
            {
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/9?lang=en",
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/22?lang=en"
            });

        // Fresh DI scope per message in production.
        FootballDataContext.ChangeTracker.Clear();

        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        // act — reprocess the same play (update path).
        await sut.ProcessAsync(CreateCommand(json, competitionId.ToString()));

        // assert
        var updated = await FootballDataContext.CompetitionPlays
            .AsNoTracking()
            .Include(x => x.ExternalIds)
            .FirstAsync(x => x.Id == canonicalId);

        // Full remap refreshed the field (previously the update path left it stale).
        updated.Text.Should().NotBe("STALE TEXT");
        // Identity + audit preserved.
        updated.Id.Should().Be(canonicalId);
        updated.CreatedUtc.Should().Be(originalCreatedUtc);
        updated.CreatedBy.Should().Be(originalCreatedBy);
        // ExternalIds is a navigation — SetValues touches scalars only, so the
        // original row is untouched (not duplicated or replaced).
        updated.ExternalIds.Should().ContainSingle()
            .Which.Id.Should().Be(originalExternalIdRowId);

        // Newly-captured columns backfilled by the full remap (seeded null; the TD
        // fixture provides them) — this is the resourcing/replay guarantee.
        updated.Wallclock.Should().Be(td.Wallclock);
        updated.ScoringTypeName.Should().Be("touchdown");
        updated.ScoringTypeDisplayName.Should().Be("Touchdown");
        updated.ScoringTypeAbbreviation.Should().Be("TD");
        updated.PointAfterAttemptId.Should().Be(61);
        updated.PointAfterAttemptText.Should().Be("Extra Point Good");
        updated.PointAfterAttemptAbbreviation.Should().Be("Extra Point Good");
        updated.PointAfterAttemptValue.Should().Be(1);
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenSeasonMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            null, // Set Season to null
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            command.ParentId,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip="log not throw")]
    public async Task WhenParentIdInvalid_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.SeasonYear,
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            "invalid-guid",
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenParentIdMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<FootballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.SeasonYear,
            command.DocumentType,
            command.Document,
            command.MessageId,
            command.CorrelationId,
            null,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }
}