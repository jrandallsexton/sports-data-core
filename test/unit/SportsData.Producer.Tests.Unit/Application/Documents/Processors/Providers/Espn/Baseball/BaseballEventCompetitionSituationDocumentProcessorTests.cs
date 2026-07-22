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
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

[Collection("Sequential")]
public class BaseballEventCompetitionSituationDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionSituationDocumentProcessor<BaseballDataContext>>
{
    private readonly BaseballDataContext _db;

    // The two baserunners (onFirst / onSecond) and the lastPlay in the fixture.
    private const string OnFirstAthleteRef = "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/athletes/41217?lang=en&region=us";
    private const string OnSecondAthleteRef = "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/athletes/41183?lang=en&region=us";

    public BaseballEventCompetitionSituationDocumentProcessorTests()
    {
        _db = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_db);
    }

    private async Task<(Guid competitionId, Guid lastPlayId)> SeedCompetitionAndLastPlayAsync(
        ExternalRefIdentityGenerator generator,
        EspnBaseballEventCompetitionSituationDto dto)
    {
        var competitionId = Guid.NewGuid();
        await _db.Competitions.AddAsync(new BaseballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });

        var lastPlayIdentity = generator.Generate(dto.LastPlay!.Ref);
        await _db.CompetitionPlays.AddAsync(new BaseballCompetitionPlay
        {
            Id = lastPlayIdentity.CanonicalId,
            CompetitionId = competitionId,
            EspnId = "4018148441704990099",
            SequenceNumber = "590",
            Text = "Test play",
            TypeId = "99",
            Type = PlayType.Unknown,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<CompetitionPlayExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionPlayId = lastPlayIdentity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = lastPlayIdentity.CleanUrl,
                    SourceUrlHash = lastPlayIdentity.UrlHash,
                    Value = lastPlayIdentity.UrlHash
                }
            }
        });

        await _db.SaveChangesAsync();
        return (competitionId, lastPlayIdentity.CanonicalId);
    }

    private Guid SeedBaserunnerAthleteSeason(ExternalRefIdentityGenerator generator, string refUrl)
    {
        var id = Guid.NewGuid();
        _db.AthleteSeasons.Add(new BaseballAthleteSeason
        {
            Id = id,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
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

    private ProcessDocumentCommand BuildCommand(string json, Guid competitionId) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionSituation)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

    [Fact]
    public async Task WhenNewBaseballSituation_PersistsCountBaserunnersAndNotes()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionSituationDto>();

        var (competitionId, lastPlayId) = await SeedCompetitionAndLastPlayAsync(generator, dto!);
        var onFirstId = SeedBaserunnerAthleteSeason(generator, OnFirstAthleteRef);
        var onSecondId = SeedBaserunnerAthleteSeason(generator, OnSecondAthleteRef);
        await _db.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(BuildCommand(json, competitionId));

        // assert
        var situation = await _db.CompetitionSituations
            .OfType<BaseballCompetitionSituation>()
            .Include(x => x.Notes)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        situation.Should().NotBeNull();
        situation!.LastPlayId.Should().Be(lastPlayId);
        situation.Balls.Should().Be(2);
        situation.Strikes.Should().Be(2);
        situation.Outs.Should().Be(1);
        situation.OnFirstAthleteSeasonId.Should().Be(onFirstId);
        situation.OnSecondAthleteSeasonId.Should().Be(onSecondId);
        situation.OnThirdAthleteSeasonId.Should().BeNull("no runner on third in the fixture");
        situation.Notes.Should().HaveCount(2);
        situation.Notes.Select(n => n.Type).Should().Contain(new[] { "RISP_STATS", "BVP_STATS" });
    }

    [Fact]
    public async Task WhenBaserunnerNotSourced_WithholdsSituation_AndRequestsAthleteSeason()
    {
        // arrange — lastPlay seeded, but NOT the baserunner athletes.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var bus = Mocker.GetMock<IEventBus>();

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionSituationDto>();

        var (competitionId, _) = await SeedCompetitionAndLastPlayAsync(generator, dto!);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<BaseballDataContext>>();

        // act — base swallows ExternalDocumentNotSourcedException and schedules a retry.
        await sut.ProcessAsync(BuildCommand(json, competitionId));

        // assert — situation withheld, AthleteSeason requested.
        (await _db.CompetitionSituations.AnyAsync(x => x.CompetitionId == competitionId))
            .Should().BeFalse("the situation must be withheld until its baserunners are sourced");

        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.AthleteSeason),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenSituationAlreadyExists_ShouldSkip()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");
        var dto = json.FromJson<EspnBaseballEventCompetitionSituationDto>();

        var (competitionId, lastPlayId) = await SeedCompetitionAndLastPlayAsync(generator, dto!);
        SeedBaserunnerAthleteSeason(generator, OnFirstAthleteRef);
        SeedBaserunnerAthleteSeason(generator, OnSecondAthleteRef);

        var identity = generator.Generate(dto!.Ref);
        await _db.Set<BaseballCompetitionSituation>().AddAsync(new BaseballCompetitionSituation
        {
            Id = identity.CanonicalId,
            CompetitionId = competitionId,
            LastPlayId = lastPlayId,
            Balls = 0,
            Strikes = 0,
            Outs = 0,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<BaseballDataContext>>();

        // act
        await sut.ProcessAsync(BuildCommand(json, competitionId));

        // assert — still exactly one record (duplicate skipped)
        (await _db.CompetitionSituations.CountAsync(x => x.CompetitionId == competitionId))
            .Should().Be(1, "duplicate situations should be skipped");
    }

    [Fact]
    public async Task WhenLastPlayNotSourced_ShouldWithholdSituation()
    {
        // arrange — competition exists but NOT the last play.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");

        var competitionId = Guid.NewGuid();
        await _db.Competitions.AddAsync(new BaseballCompetition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await _db.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<BaseballDataContext>>();

        // act — ExternalDocumentNotSourcedException caught by the base as a retry.
        await sut.ProcessAsync(BuildCommand(json, competitionId));

        // assert — not created while the lastPlay dependency is missing.
        (await _db.CompetitionSituations.CountAsync(x => x.CompetitionId == competitionId))
            .Should().Be(0, "situation should not be created when last play dependency is missing");
    }
}
