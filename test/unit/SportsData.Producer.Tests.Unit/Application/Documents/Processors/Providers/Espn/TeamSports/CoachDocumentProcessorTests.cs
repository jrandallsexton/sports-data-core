using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class CoachDocumentProcessorTests : ProducerTestBase<CoachDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task WhenCoachDoesNotExist_ShouldCreateIt()
    {
        // Arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<CoachDocumentProcessor<TeamSportDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaCoach.json");
        var url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872?lang=en&region=us";
        var urlHash = HashProvider.UrlHash(url);
        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Coach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, urlHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var created = await TeamSportDataContext.Coaches
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.FirstName == "Brian" && x.LastName == "Kelly");
        created.Should().NotBeNull();
        created!.Experience.Should().Be(25);
        created.ExternalIds.Should().ContainSingle(e => e.Value == urlHash);
    }

    [Fact]
    public async Task WhenCoachExists_ShouldUpdateExperience()
    {
        // Arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872?lang=en&region=us";
        var urlHash = HashProvider.UrlHash(url);
        var existing = new Coach
        {
            Id = Fixture.Create<Guid>(),
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 10,
            CreatedUtc = System.DateTime.UtcNow,
            CreatedBy = Fixture.Create<Guid>(),
            ExternalIds =
            [
                new CoachExternalId
                {
                    Id = Fixture.Create<Guid>(),
                    Provider = SourceDataProvider.Espn,
                    Value = urlHash,
                    SourceUrlHash = urlHash,
                    SourceUrl = url
                }
            ]
        };
        await TeamSportDataContext.Coaches.AddAsync(existing);
        await TeamSportDataContext.SaveChangesAsync();
        var sut = Mocker.CreateInstance<CoachDocumentProcessor<TeamSportDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaCoach.json");
        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Coach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, urlHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var updated = await TeamSportDataContext.Coaches
            .FirstOrDefaultAsync(x => x.FirstName == "Brian" && x.LastName == "Kelly");
        updated.Should().NotBeNull();
        updated!.Experience.Should().Be(25);
    }
}
