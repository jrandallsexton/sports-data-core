using AutoFixture;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionDriveDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    [Fact]
    public async Task WhenEntityDoesNotExist_ContestExists_ShouldAddDrive()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionDrive)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var created = await FootballDataContext.Drives.FirstOrDefaultAsync();
        created.Should().NotBeNull();

        // Verify additional properties as needed based on your data model
        // created!.SomeProperty.Should().Be(expectedValue);

        // Verify events published
        // bus.Verify(x => x.Publish(It.IsAny<YourEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenEntityAlreadyExists_ShouldSkipCreation_AndNotPublishDriveEvents()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");
        
        // TODO: Add existing drive to context
        // var existingDrive = Fixture.Build<ContestDrive>()
        //     .With(x => /* set required properties */)
        //     .Create();
        // await FootballDataContext.ContestDrives.AddAsync(existingDrive);
        // await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.EventCompetitionDrive)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        // Verify no drive creation events were published
        // bus.Verify(x => x.Publish(It.IsAny<YourDriveEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}