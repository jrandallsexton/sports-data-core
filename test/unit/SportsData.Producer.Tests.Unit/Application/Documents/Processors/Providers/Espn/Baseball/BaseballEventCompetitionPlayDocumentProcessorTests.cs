#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for <see cref="BaseballEventCompetitionPlayDocumentProcessor{TDataContext}"/>.
///
/// Coverage focus is the capture-fidelity work and TPH wiring:
///   • DTO deserialization of the rich per-play shape (pitches, bats,
///     pitchCoordinate, hitCoordinate, participants, etc.).
///   • Status-gated publish: missing status throws, completed games
///     persist but don't publish, in-progress games persist + publish.
///   • Participant capture into the shared CompetitionPlayParticipant
///     TPH table, including AthleteSeasonId / PositionId resolution, race
///     handling (null on unsourced refs), and forward-compat warning
///     on unrecognized participant types.
///   • ApplyUpdateAsync: re-resolves athletes, refreshes rich fields,
///     and replaces the participants set wholesale.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionPlayDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>
{
    private readonly BaseballDataContext _baseballDataContext;
    private static readonly DateTime FixedNow = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    public BaseballEventCompetitionPlayDocumentProcessorTests()
    {
        _baseballDataContext = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_baseballDataContext);
    }

    [Fact]
    public async Task DTO_Deserializes_RichFields_From_ScoringFixture()
    {
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay_Scoring.json");

        var dto = json.FromJson<EspnBaseballEventCompetitionPlayDto>();

        dto.Should().NotBeNull();
        dto!.Valid.Should().BeTrue();
        dto.AtBatId.Should().Be("4018148440202");
        dto.BatOrder.Should().Be(5);
        dto.Bats!.Type.Should().Be("LEFT");
        dto.Bats.Abbreviation.Should().Be("L");
        dto.Pitches!.Type.Should().Be("RIGHT");
        dto.Pitches.Abbreviation.Should().Be("R");
        dto.AtBatPitchNumber.Should().Be(2);
        dto.PitchCoordinate!.X.Should().Be(113);
        dto.PitchCoordinate.Y.Should().Be(159);
        dto.HitCoordinate!.X.Should().Be(205);
        dto.HitCoordinate.Y.Should().Be(62);
        dto.SummaryType.Should().Be("S");
        dto.PitchCount!.Balls.Should().Be(1);
        dto.ResultCount!.Strikes.Should().Be(0);
        dto.Outs.Should().Be(1);
        dto.Trajectory.Should().Be("F");
        dto.RbiCount.Should().Be(1);
        dto.Period!.Type.Should().Be("Top");
        dto.Period.DisplayValue.Should().Be("2nd Inning");
        dto.Participants.Should().HaveCount(2);
        dto.Participants![0].Type.Should().Be("pitcher");
        dto.Participants[1].Type.Should().Be("batter");
    }

    [Fact]
    public async Task WhenStatusMissing_ThrowsForRetry()
    {
        // arrange — competition exists but no BaseballCompetitionStatus row.
        // Tri-state IsCompetitionInProgressAsync returns null → base throws
        // so MassTransit retries once status is sourced.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay.json",
            seedStatus: null);

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act + assert
        await FluentActions.Awaiting(() => sut.ProcessAsync(cmd))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status not yet sourced*");

        var plays = await _baseballDataContext.CompetitionPlays
            .Where(p => p.CompetitionId == competitionId).ToListAsync();
        plays.Should().BeEmpty("nothing should persist when the gating throw fires");
    }

    [Fact]
    public async Task WhenInProgress_CreatesPlayAndPublishesPlayCompleted()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, contestId) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — play persisted with the rich-capture columns populated
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.Text.Should().Be("Top of the 1st inning");
        play.TypeId.Should().Be("59");
        play.PeriodNumber.Should().Be(1);
        play.HalfInning.Should().Be("Top");
        play.Outs.Should().Be(0);
        play.IsValid.Should().BeTrue();
        play.AtBatId.Should().Be("4018148440001");
        play.SummaryType.Should().Be("I");

        // start-inning has no participants in the JSON
        play.Participants.Should().BeEmpty();
        play.AtBatAthleteSeasonId.Should().BeNull();
        play.PitchingAthleteSeasonId.Should().BeNull();

        // BaseballPlayCompleted published once
        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.Is<BaseballPlayCompleted>(e =>
                    e.CompetitionId == competitionId &&
                    e.ContestId == contestId &&
                    e.PlayId == play.Id),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenCompleted_CreatesPlayButDoesNotPublish()
    {
        // arrange — Final game: status exists with IsCompleted=true.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay.json",
            seedStatus: BaseballStatus(isCompleted: true));

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — play persisted, no PlayCompleted event published
        var play = await _baseballDataContext.CompetitionPlays
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();

        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.IsAny<BaseballPlayCompleted>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenScoringPlay_CapturesAllRichFields()
    {
        // arrange — scoring-play fixture has the wide field shape: bats,
        // pitches, pitchCoordinate, hitCoordinate, trajectory, etc.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay_Scoring.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — the AsBaseballEntity extension populates every column
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ScoringPlay.Should().BeTrue();
        play.ScoreValue.Should().Be(1);
        play.HalfInning.Should().Be("Top");
        play.Outs.Should().Be(1);
        play.AtBatId.Should().Be("4018148440202");
        play.BatOrder.Should().Be(5);
        play.BatsType.Should().Be("LEFT");
        play.BatsAbbreviation.Should().Be("L");
        play.PitchesType.Should().Be("RIGHT");
        play.PitchesAbbreviation.Should().Be("R");
        play.AtBatPitchNumber.Should().Be(2);
        play.PitchCoordinateX.Should().Be(113);
        play.PitchCoordinateY.Should().Be(159);
        play.HitCoordinateX.Should().Be(205);
        play.HitCoordinateY.Should().Be(62);
        play.Trajectory.Should().Be("F");
        play.SummaryType.Should().Be("S");
        play.RbiCount.Should().Be(1);
        play.AwayHits.Should().Be(1);
        play.AwayErrors.Should().Be(1);
        play.IsValid.Should().BeTrue();
        play.Wallclock.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenParticipantsResolve_PopulatesAthleteSeasonAndPositionIds()
    {
        // arrange — scoring fixture's participants[] has pitcher + batter
        // with season-scoped athlete refs + position refs. Seed canonical
        // AthleteSeason + AthletePosition rows so resolution succeeds.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay_Scoring.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var pitcherAthleteSeasonId = await SeedAthleteSeasonAsync(generator,
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/athletes/4345076?lang=en&region=us");
        var batterAthleteSeasonId = await SeedAthleteSeasonAsync(generator,
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/athletes/4917812?lang=en&region=us");
        var pitcherPositionId = await SeedPositionAsync(generator,
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/positions/15?lang=en&region=us");
        var batterPositionId = await SeedPositionAsync(generator,
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/positions/2?lang=en&region=us");

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — denormalized columns on the play row carry the IDs and
        // the participants table has both rows fully resolved.
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .Include(p => p.Participants)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.AtBatAthleteSeasonId.Should().Be(batterAthleteSeasonId);
        play.PitchingAthleteSeasonId.Should().Be(pitcherAthleteSeasonId);
        play.Participants.Should().HaveCount(2);

        var pitcherRow = play.Participants.Single(p => p.Type == "pitcher");
        pitcherRow.AthleteSeasonId.Should().Be(pitcherAthleteSeasonId);
        pitcherRow.PositionId.Should().Be(pitcherPositionId);
        pitcherRow.Order.Should().Be(1);

        var batterRow = play.Participants.Single(p => p.Type == "batter");
        batterRow.AthleteSeasonId.Should().Be(batterAthleteSeasonId);
        batterRow.PositionId.Should().Be(batterPositionId);
        batterRow.Order.Should().Be(2);

        // Display payload (ShortName / position abbreviation / headshot) is
        // hydrated on the publish path so SignalR consumers don't round-trip
        // for athlete data per play. Verify it lands on the emitted event.
        Mock.Get(Mocker.Get<IEventBus>())
            .Verify(x => x.Publish(
                It.Is<BaseballPlayCompleted>(e =>
                    e.AtBatAthleteSeasonId == batterAthleteSeasonId &&
                    e.PitchingAthleteSeasonId == pitcherAthleteSeasonId &&
                    e.AtBatShortName == "Test Athlete 2026" &&
                    e.PitchingShortName == "Test Athlete 2026" &&
                    e.AtBatPositionAbbreviation == "T" &&
                    e.PitchingPositionAbbreviation == "T"),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenAthleteSeasonsUnresolved_PersistsParticipantsWithNullIds()
    {
        // arrange — scoring fixture has participants but we deliberately do
        // not seed any AthleteSeason / AthletePosition rows. ResolveIdAsync
        // returns null; participant rows still persist for later re-resolve
        // on the next play update.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var (json, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay_Scoring.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .Include(p => p.Participants)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.AtBatAthleteSeasonId.Should().BeNull();
        play.PitchingAthleteSeasonId.Should().BeNull();
        play.Participants.Should().HaveCount(2, "rows persist even when refs aren't resolved yet");
        play.Participants.Should().OnlyContain(p =>
            p.AthleteSeasonId == null && p.PositionId == null);
    }

    [Fact]
    public async Task WhenUnrecognizedParticipantType_StillPersistsRow()
    {
        // arrange — mutate the scoring fixture so one participant has an
        // unknown Type. The unknown-type branch logs a warning but does
        // not drop the row (forward-compat: ESPN can grow its taxonomy).
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var rawJson = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay_Scoring.json");
        var dto = rawJson.FromJson<EspnBaseballEventCompetitionPlayDto>()!;
        dto.Participants![1].Type = "fielder";  // was "batter"
        var mutatedJson = dto.ToJson();

        var (_, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay_Scoring.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var cmd = BuildCommand(mutatedJson, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — both rows still landed; only pitcher counted as primary.
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .Include(p => p.Participants)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.Participants.Should().HaveCount(2);
        play.Participants.Should().Contain(p => p.Type == "fielder");
        play.AtBatAthleteSeasonId.Should().BeNull("'fielder' isn't recognized as the batter slot");
    }

    [Fact]
    public async Task WhenPlayAlreadyExists_UpdatesFieldsAndReplacesParticipants()
    {
        // arrange — seed an existing baseball play with stale data + one
        // participant row, then re-process the scoring fixture.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay_Scoring.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionPlayDto>()!;
        var playIdentity = generator.Generate(dto.Ref);

        var (_, competitionId, _) = await PrepareScenarioAsync(
            "EspnBaseballMlb/EventCompetitionPlay_Scoring.json",
            seedStatus: BaseballStatus(isCompleted: false));

        var existing = new BaseballCompetitionPlay
        {
            Id = playIdentity.CanonicalId,
            CompetitionId = competitionId,
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            Text = "stale",
            TypeId = "0",
            Type = PlayType.Unknown,
            Modified = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            BatOrder = 99,
            PitchVelocity = 999,
            ExternalIds = new List<CompetitionPlayExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionPlayId = playIdentity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = playIdentity.CleanUrl,
                    SourceUrlHash = playIdentity.UrlHash,
                    Value = playIdentity.UrlHash
                }
            }
        };
        await _baseballDataContext.CompetitionPlays.AddAsync(existing);

        // pre-existing stale participant row (different athlete) — should be wiped.
        var staleAthleteSeasonId = Guid.NewGuid();
        await _baseballDataContext.Set<BaseballCompetitionPlayParticipant>().AddAsync(
            new BaseballCompetitionPlayParticipant
            {
                Id = Guid.NewGuid(),
                CompetitionPlayId = playIdentity.CanonicalId,
                Order = 9,
                Type = "stale-type",
                AthleteSeasonId = staleAthleteSeasonId,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        await _baseballDataContext.SaveChangesAsync();

        var cmd = BuildCommand(json, competitionId);
        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(cmd);

        // assert — fields refreshed from the new payload and the stale
        // participant is gone, replaced by the JSON's two participants.
        var play = await _baseballDataContext.CompetitionPlays
            .OfType<BaseballCompetitionPlay>()
            .Include(p => p.Participants)
            .AsNoTracking()
            .FirstAsync(x => x.Id == playIdentity.CanonicalId);

        play.BatOrder.Should().Be(5);
        play.PitchesType.Should().Be("RIGHT");
        play.HitCoordinateX.Should().Be(205);
        play.Participants.Should().HaveCount(2);
        play.Participants.Should().NotContain(p => p.AthleteSeasonId == staleAthleteSeasonId);
        play.Participants.Should().OnlyContain(p =>
            p.Type == "pitcher" || p.Type == "batter");
    }

    // ---- helpers ----

    private async Task<(string json, Guid competitionId, Guid contestId)> PrepareScenarioAsync(
        string fixturePath,
        BaseballCompetitionStatus? seedStatus)
    {
        var json = await LoadJsonTestData(fixturePath);
        var dto = json.FromJson<EspnBaseballEventCompetitionPlayDto>()!;

        var competitionId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        await _baseballDataContext.Contests.AddAsync(new BaseballContest
        {
            Id = contestId,
            Name = "Test Contest",
            ShortName = "Test",
            SeasonYear = 2026,
            Sport = Sport.BaseballMlb,
            StartDateUtc = FixedNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        await _baseballDataContext.Competitions.AddAsync(new BaseballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            Date = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        if (seedStatus is not null)
        {
            seedStatus.CompetitionId = competitionId;
            await _baseballDataContext.Set<BaseballCompetitionStatus>().AddAsync(seedStatus);
        }

        await _baseballDataContext.SaveChangesAsync();

        return (json, competitionId, contestId);
    }

    private static BaseballCompetitionStatus BaseballStatus(bool isCompleted) =>
        new()
        {
            Id = Guid.NewGuid(),
            IsCompleted = isCompleted,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

    private async Task<Guid> SeedAthleteSeasonAsync(ExternalRefIdentityGenerator generator, string athleteSeasonRef)
    {
        // Seed a global AthleteBase + AthletePosition (required FKs) then
        // the season-scoped AthleteSeason with its ExternalId. The play
        // processor resolves against AthleteSeasonExternalId, so only the
        // season-scoped URL hash needs to match.
        var identity = generator.Generate(athleteSeasonRef);
        var athleteSeasonId = identity.CanonicalId;

        var parentAthleteId = Guid.NewGuid();
        await _baseballDataContext.Athletes.AddAsync(new BaseballAthlete
        {
            Id = parentAthleteId,
            FirstName = "Test",
            LastName = "Athlete",
            DisplayName = "Test Athlete",
            ShortName = "T. Athlete",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        var parentPositionId = Guid.NewGuid();
        await _baseballDataContext.AthletePositions.AddAsync(new AthletePosition
        {
            Id = parentPositionId,
            Name = "Parent",
            DisplayName = "Parent Position",
            Abbreviation = "P",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        await _baseballDataContext.AthleteSeasons.AddAsync(new BaseballAthleteSeason
        {
            Id = athleteSeasonId,
            AthleteId = parentAthleteId,
            PositionId = parentPositionId,
            DisplayName = "Test Athlete 2026",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthleteSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthleteSeasonId = athleteSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    Value = identity.UrlHash
                }
            }
        });
        await _baseballDataContext.SaveChangesAsync();
        return athleteSeasonId;
    }

    private async Task<Guid> SeedPositionAsync(ExternalRefIdentityGenerator generator, string positionRef)
    {
        var identity = generator.Generate(positionRef);
        var positionId = identity.CanonicalId;

        await _baseballDataContext.AthletePositions.AddAsync(new AthletePosition
        {
            Id = positionId,
            Name = "Test",
            DisplayName = "Test Position",
            Abbreviation = "T",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    AthletePositionId = positionId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    Value = identity.UrlHash
                }
            }
        });
        await _baseballDataContext.SaveChangesAsync();
        return positionId;
    }

    private ProcessDocumentCommand BuildCommand(string json, Guid competitionId) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.ParentId, competitionId.ToString())
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.Document, json)
            .OmitAutoProperties()
            .Create();
}
