﻿using AutoFixture;

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
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var existingId = Guid.NewGuid();
        var existingUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/70";
        var existingHash = existingUrl.UrlHash();

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
                    SourceUrlHash = existingHash,
                    SourceUrl = existingUrl
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<AthletePositionDocumentProcessor<FootballDataContext>>();

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
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var existingId = Guid.NewGuid();
        var existingUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/1";
        var existingHash = existingUrl.UrlHash();

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
                    SourceUrlHash = existingHash,
                    SourceUrl = existingUrl
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<AthletePositionDocumentProcessor<FootballDataContext>>();

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
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

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
                    AthletePositionId = existingId,
                    SourceUrl = "http://sports.core.api.espn.com/someFakeUrl"
                }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(existingPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<AthletePositionDocumentProcessor<FootballDataContext>>();

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

    [Fact]
    public async Task WhenParentExists_ShouldResolveAndSetParentId()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();

        var parentUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/70";
        var parentHash = parentUrl.UrlHash();

        var parentId = Guid.NewGuid();
        var parentPosition = new AthletePosition
        {
            Id = parentId,
            Name = "Offense",
            DisplayName = "Offense",
            Abbreviation = "OFF",
            ExternalIds =
            [
                new AthletePositionExternalId
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = parentHash,
                SourceUrlHash = parentHash,
                SourceUrl = parentUrl
            }
            ]
        };

        await FootballDataContext.AthletePositions.AddAsync(parentPosition);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<AthletePositionDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballAthletePosition.json"); // assumes this has a valid "Parent.Ref"

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
            .FirstOrDefaultAsync(x => x.Name == "Wide Receiver");

        created.Should().NotBeNull();
        created!.ParentId.Should().Be(parentId);

        bus.Verify(x => x.Publish(It.IsAny<AthletePositionCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenParentDoesNotExist_ShouldThrowAndNotSave()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var logger = Mocker.GetMock<ILogger<AthletePositionDocumentProcessor<FootballDataContext>>>();
        
        var sut = Mocker.CreateInstance<AthletePositionDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballAthletePosition.json"); // includes Parent.Ref to a position that does NOT exist

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.AthletePosition)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/positions/1".UrlHash())
            .OmitAutoProperties()
            .Create();

        // Act
        Func<Task> act = async () => await sut.ProcessAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Parent position not yet available*");

        var created = await FootballDataContext.AthletePositions
            .FirstOrDefaultAsync(x => x.Name == "Wide Receiver");

        created.Should().BeNull("we should not persist when parent is unresolved");

        bus.Verify(x => x.Publish(It.IsAny<AthletePositionCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
