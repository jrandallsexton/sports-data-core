using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using System.Text.Json;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class EventCompetitionProbabilityDocumentProcessorTests :
    ProducerTestBase<EventCompetitionProbabilityDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task WhenEntityDoesNotExist_IsAdded()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionProbability.json");
        var dto = JsonSerializer.Deserialize<EspnEventCompetitionProbabilityDto>(documentJson)!;

        var competitionIdentity = generator.Generate(dto.Competition.Ref!);
        var playIdentity = generator.Generate(dto.Play.Ref!);

        var competition = Fixture.Build<Competition>()
            .OmitAutoProperties()
            .With(x => x.Id, competitionIdentity.CanonicalId)
            .With(x => x.Probabilities, new List<CompetitionProbability>())
            .With(x => x.ExternalIds, new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = competitionIdentity.CleanUrl,
                    SourceUrlHash = competitionIdentity.UrlHash,
                    Value = competitionIdentity.UrlHash
                }
            })
            .Create();

        var play = Fixture.Build<Play>()
            .OmitAutoProperties()
            .With(x => x.Id, playIdentity.CanonicalId)
            .With(x => x.EspnId, "0")
            .With(x => x.SequenceNumber, "0")
            .With(x => x.Text, "Smith ran to the right for 3 yards")
            .With(x => x.TypeId, "typeId")
            .With(x => x.ExternalIds, new List<PlayExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = playIdentity.CleanUrl,
                    SourceUrlHash = playIdentity.UrlHash,
                    Value = playIdentity.UrlHash
                }
            })
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.Plays.AddAsync(play);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.EventCompetitionProbability)
            .With(x => x.Document, documentJson)
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<EventCompetitionProbabilityDocumentProcessor<TeamSportDataContext>>();

        // act
        await sut.ProcessAsync(command);

        // assert
        var entity = await FootballDataContext.CompetitionProbabilities
            .AsNoTracking()
            .FirstOrDefaultAsync();

        entity.Should().NotBeNull();
        entity!.CompetitionId.Should().Be(competition.Id);
        entity.PlayId.Should().Be(play.Id);
        entity.HomeWinPercentage.Should().Be(dto.HomeWinPercentage);
        entity.AwayWinPercentage.Should().Be(dto.AwayWinPercentage);
        entity.TiePercentage.Should().Be(dto.TiePercentage);
        entity.SecondsLeft.Should().Be(dto.SecondsLeft);
    }

    [Fact(Skip="TODO")]
    public async Task WhenValuesHaveNotChanged_SecondCallIsSkipped()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionProbability.json");
        var dto = JsonSerializer.Deserialize<EspnEventCompetitionProbabilityDto>(documentJson)!;

        var competitionIdentity = generator.Generate(dto.Competition.Ref!);
        var playIdentity = generator.Generate(dto.Play.Ref!);

        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, competitionIdentity.CanonicalId)
            .With(x => x.ExternalIds, new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = competitionIdentity.CleanUrl,
                    SourceUrlHash = competitionIdentity.UrlHash,
                    Value = competitionIdentity.UrlHash
                }
            })
            .Create();

        var play = Fixture.Build<Play>()
            .With(x => x.Id, playIdentity.CanonicalId)
            .With(x => x.ExternalIds, new List<PlayExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = playIdentity.CleanUrl,
                    SourceUrlHash = playIdentity.UrlHash,
                    Value = playIdentity.UrlHash
                }
            })
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.Plays.AddAsync(play);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.EventCompetitionProbability)
            .With(x => x.Document, documentJson)
            .OmitAutoProperties()
            .Create();

        var sut = Mocker.CreateInstance<EventCompetitionProbabilityDocumentProcessor<TeamSportDataContext>>();

        // act: first call should persist
        await sut.ProcessAsync(command);

        // act: second call should detect no delta and skip
        await sut.ProcessAsync(command);

        // assert
        var count = await FootballDataContext.CompetitionProbabilities.CountAsync();
        count.Should().Be(1);
    }
}
