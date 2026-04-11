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
public class BaseballEventCompetitionPlayDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private async Task<(Guid competitionId, Guid teamFranchiseSeasonId)> SetupTestDataAsync(
        ExternalRefIdentityGenerator generator,
        string teamRef)
    {
        var competitionId = Guid.NewGuid();
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        var teamIdentity = generator.Generate(teamRef);
        var franchiseSeasonId = Guid.NewGuid();
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2026,
            Abbreviation = "KC",
            DisplayName = "Kansas City Royals",
            DisplayNameShort = "Royals",
            Location = "Kansas City",
            Name = "Royals",
            Slug = "kansas-city-royals",
            ColorCodeHex = "#004687",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = franchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = teamIdentity.CleanUrl,
                    SourceUrlHash = teamIdentity.UrlHash,
                    Value = teamIdentity.UrlHash
                }
            }
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        return (competitionId, franchiseSeasonId);
    }

    [Fact]
    public async Task WhenNewBaseballPlay_ShouldCreateWithNullDriveAndStartEnd()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay.json");
        var dto = json.FromJson<EspnEventCompetitionPlayDto>();

        var teamRef = dto!.Team.Ref.ToString();
        var (competitionId, teamFranchiseSeasonId) = await SetupTestDataAsync(generator, teamRef);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.DriveId.Should().BeNull("baseball has no drives");
        play.StartDown.Should().BeNull("baseball has no downs");
        play.StartDistance.Should().BeNull("baseball has no distance");
        play.StartYardLine.Should().BeNull("baseball has no yard lines");
        play.EndDown.Should().BeNull();
        play.EndFranchiseSeasonId.Should().BeNull("baseball plays have no end team");
        play.StartFranchiseSeasonId.Should().Be(teamFranchiseSeasonId);
        play.Text.Should().Be("Top of the 1st inning");
        play.PeriodNumber.Should().Be(1);
        // TypeId preserves the raw ESPN type ID for future baseball-specific enum mapping.
        // PlayType enum currently only has football values; baseball IDs that collide
        // (e.g., 59 = "Start Inning" in baseball vs "FieldGoalGood" in football)
        // will parse to the wrong football value. This is acceptable until we add
        // sport-scoped play type enums.
        play.TypeId.Should().Be("59");
    }

    [Fact]
    public async Task WhenScoringPlay_ShouldCreateWithScoreData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay_Scoring.json");
        var dto = json.FromJson<EspnEventCompetitionPlayDto>();

        var teamRef = dto!.Team.Ref.ToString();
        var (competitionId, _) = await SetupTestDataAsync(generator, teamRef);

        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ScoringPlay.Should().BeTrue();
        play.ScoreValue.Should().BeGreaterThan(0);
        play.Text.Should().Contain("homered");
    }

    [Fact]
    public async Task WhenPlayAlreadyExists_ShouldUpdate()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionPlay.json");
        var dto = json.FromJson<EspnEventCompetitionPlayDto>();

        var teamRef = dto!.Team.Ref.ToString();
        var (competitionId, teamFranchiseSeasonId) = await SetupTestDataAsync(generator, teamRef);

        var playIdentity = generator.Generate(dto.Ref);

        // Pre-create the play
        var existingPlay = new CompetitionPlay
        {
            Id = playIdentity.CanonicalId,
            CompetitionId = competitionId,
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            Text = "old text",
            TypeId = "59",
            Type = PlayType.Unknown,
            Modified = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
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
        await FootballDataContext.CompetitionPlays.AddAsync(existingPlay);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<BaseballEventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SeasonYear, 2026)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.ParentId, competitionId.ToString())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

        play.Should().NotBeNull();
        play!.StartFranchiseSeasonId.Should().Be(teamFranchiseSeasonId);
    }
}
