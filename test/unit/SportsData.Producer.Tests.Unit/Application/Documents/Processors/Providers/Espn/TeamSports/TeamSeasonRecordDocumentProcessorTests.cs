using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using System.Text.Json;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class TeamSeasonRecordDocumentProcessorTests
        : ProducerTestBase<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task ProcessAsync_AddsRecords_WhenFranchiseSeasonExists_AndItemsExist()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2024)
                .With(x => x.Records, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonRecord.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var savedRecords = await TeamSportDataContext.FranchiseSeasonRecords
                .Include(r => r.Stats)
                .Where(r => r.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            savedRecords.Should().HaveCount(4, "JSON document has 4 items to process");

            // Spot-check for specific known values
            savedRecords.Should().Contain(r => r.Name == "overall" && r.Type == "total");
            savedRecords.Should().Contain(r => r.Name == "Home" && r.Type == "homerecord");

            // Check example value accuracy (if your mapping includes Value or WinPercent)
            var overallRecord = savedRecords.FirstOrDefault(r => r.Name == "overall");
            overallRecord.Should().NotBeNull();
            overallRecord.Stats.Should().NotBeNull();
            var winPercent = overallRecord.Stats.FirstOrDefault(s => s.Name.ToLowerInvariant() == "winpercent");
            winPercent.Should().NotBeNull();
            winPercent.Value.Should().BeApproximately(0.692, 0.01);

            // Verify publish count matches
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Exactly(savedRecords.Count));
        }


        [Fact]
        public async Task ProcessAsync_DoesNothing_WhenFranchiseSeasonNotFound()
        {
            // Arrange
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, "{}")
                .With(x => x.ParentId, Guid.NewGuid().ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonRecords.CountAsync()).Should().Be(0);
            bus.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessAsync_DoesNothing_WhenParentIdInvalid()
        {
            // Arrange
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, "{}")
                .With(x => x.ParentId, "not-a-guid")
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonRecords.CountAsync()).Should().Be(0);
            bus.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ProcessAsync_DoesNothing_WhenNoItemsInDocument()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2024)
                .With(x => x.Records, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var emptyJson = JsonSerializer.Serialize(new { items = new List<object>() });

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, emptyJson)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonRecords.CountAsync()).Should().Be(0);
            bus.VerifyNoOtherCalls();
        }

    }
}
