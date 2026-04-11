#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

[Collection("Sequential")]
public class BaseballEventCompetitionSituationDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private async Task<(Guid competitionId, Guid lastPlayId)> SetupTestDataAsync(
        ExternalRefIdentityGenerator generator,
        EspnEventCompetitionSituationDto dto)
    {
        var competitionId = Guid.NewGuid();
        await FootballDataContext.Competitions.AddAsync(new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();

        // Create the last play so the situation can resolve it
        var lastPlayIdentity = generator.Generate(dto.LastPlay.Ref);
        var lastPlay = new CompetitionPlay
        {
            Id = lastPlayIdentity.CanonicalId,
            CompetitionId = competitionId,
            EspnId = "4018148441704990099",
            SequenceNumber = "590",
            Text = "Test play",
            TypeId = "99",
            Type = PlayType.Unknown,
            Modified = DateTime.UtcNow,
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
        };
        await FootballDataContext.CompetitionPlays.AddAsync(lastPlay);
        await FootballDataContext.SaveChangesAsync();

        return (competitionId, lastPlayIdentity.CanonicalId);
    }

    [Fact]
    public async Task WhenNewBaseballSituation_ShouldCreateWithZeroedFootballFields()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");
        var dto = json.FromJson<EspnEventCompetitionSituationDto>();

        var (competitionId, lastPlayId) = await SetupTestDataAsync(generator, dto!);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionSituation)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var situation = await FootballDataContext.CompetitionSituations
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        situation.Should().NotBeNull();
        situation!.CompetitionId.Should().Be(competitionId);
        situation.LastPlayId.Should().Be(lastPlayId);
        situation.Down.Should().Be(0, "baseball has no downs");
        situation.Distance.Should().Be(0, "baseball has no distance");
        situation.YardLine.Should().Be(0, "baseball has no yard lines");
        situation.IsRedZone.Should().BeFalse();
    }

    [Fact]
    public async Task WhenSituationAlreadyExists_ShouldSkip()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");
        var dto = json.FromJson<EspnEventCompetitionSituationDto>();

        var (competitionId, lastPlayId) = await SetupTestDataAsync(generator, dto!);

        // Pre-create the situation
        var identity = generator.Generate(dto!.Ref);
        await FootballDataContext.CompetitionSituations.AddAsync(new CompetitionSituation
        {
            Id = identity.CanonicalId,
            CompetitionId = competitionId,
            LastPlayId = lastPlayId,
            Down = 0,
            Distance = 0,
            YardLine = 0,
            IsRedZone = false,
            AwayTimeouts = 0,
            HomeTimeouts = 0,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionSituation)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert — should still be exactly 1 record
        var count = await FootballDataContext.CompetitionSituations
            .CountAsync(x => x.CompetitionId == competitionId);

        count.Should().Be(1, "duplicate situations should be skipped");
    }

    [Fact]
    public async Task WhenLastPlayNotSourced_ShouldThrowExternalDocumentNotSourcedException()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionSituation.json");

        // Create competition but NOT the last play
        var competitionId = Guid.NewGuid();
        await FootballDataContext.Competitions.AddAsync(new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionSituationDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionSituation)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act — ExternalDocumentNotSourcedException is caught by DocumentProcessorBase
        // and handled as a retry, so no exception propagates
        await sut.ProcessAsync(command);

        // assert — situation should NOT be created since the dependency isn't ready
        var count = await FootballDataContext.CompetitionSituations
            .CountAsync(x => x.CompetitionId == competitionId);

        count.Should().Be(0, "situation should not be created when last play dependency is missing");
    }
}
