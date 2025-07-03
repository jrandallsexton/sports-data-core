using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class AthletePositionDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    [Fact]
    public async Task WhenPositionDoesNotExist_ShouldCreateItAndPublishCreatedEvent()
    {
        // Arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var existingId = Guid.NewGuid();
        var existingHash = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/70".UrlHash();

        var existingPosition = new AthletePosition
        {
            Id = existingId,
            Name = "Offense",
            DisplayName = "Offense",
            Abbreviation = "OFF",
            ExternalIds = [
                new AthletePositionExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = existingHash,
                    SourceUrlHash = existingHash
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = new AthletePositionDocumentProcessor<FootballDataContext>(
            logger.Object,
            FootballDataContext,
            bus.Object
        );

        var json = await LoadJsonTestData("EspnFootballAthletePosition.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.AthletePosition)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/1".UrlHash())
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var created = await FootballDataContext.AthletePositions
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Name == "Wide Receiver");

        created.Should().NotBeNull();
        created!.Name.Should().Be("Wide Receiver");
        created.DisplayName.Should().Be("Wide Receiver");
        created.Abbreviation.Should().Be("WR");

        created.ExternalIds.Should().ContainSingle(x =>
            x.Provider == SourceDataProvider.Espn &&
            x.SourceUrlHash == command.UrlHash);

        bus.Verify(x => x.Publish(It.IsAny<AthletePositionCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenPositionAlreadyExistsByExternalId_ShouldNotCreateOrPublish()
    {
        // Arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var existingId = Guid.NewGuid();
        var existingHash = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/1".UrlHash();

        var existingPosition = new AthletePosition
        {
            Id = existingId,
            Name = "Wide Receiver",
            DisplayName = "Wide Receiver",
            Abbreviation = "WR",
            ExternalIds = [
                new AthletePositionExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = existingHash,
                    SourceUrlHash = existingHash
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = new AthletePositionDocumentProcessor<FootballDataContext>(
            logger.Object,
            FootballDataContext,
            bus.Object
        );

        var json = await LoadJsonTestData("EspnFootballAthletePosition.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.AthletePosition)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, existingHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var count = await FootballDataContext.AthletePositions.CountAsync();
        count.Should().Be(1);

        bus.Verify(x => x.Publish(It.IsAny<AthletePositionCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task WhenPositionExistsByCanonicalName_ShouldAddNewExternalId()
    {
        // Arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var existingId = Guid.NewGuid();
        var existingPosition = new AthletePosition
        {
            Id = existingId,
            Name = "Wide Receiver",
            DisplayName = "Wide Receiver",
            Abbreviation = "WR",
            ExternalIds =
            [
                new AthletePositionExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = "SOMEOTHERHASH",
                    SourceUrlHash = "SOMEOTHERHASH",
                    AthletePositionId = existingId
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = new AthletePositionDocumentProcessor<FootballDataContext>(
            logger.Object,
            FootballDataContext,
            bus.Object
        );

        var json = await LoadJsonTestData("EspnFootballAthletePosition.json");
        var newHash = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/99".UrlHash();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.AthletePosition)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, newHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var positions = await FootballDataContext.AthletePositions
            .Include(x => x.ExternalIds)
            .ToListAsync();

        positions.Should().HaveCount(1, "we should not create a duplicate AthletePosition if the Name matches canonically");

        var position = positions.Single();

        position.Name.Should().Be("Wide Receiver", "the Name should be stored in canonical form");

        position.ExternalIds.Count.Should().Be(2);

        // Ensure no new AthletePositionCreated event is published
        bus.Verify(x => x.Publish(It.IsAny<AthletePositionCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
