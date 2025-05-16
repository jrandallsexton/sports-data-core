using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Application.Slugs;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class GroupBySeasonDocumentProcessorTests : UnitTestBase<GroupBySeasonDocumentProcessor>
    {
        [Fact]
        public async Task WhenNeitherGroupNorSeasonExist_BothAreCreated()
        {
            // arrange
            Mocker.GetMock<ISlugGenerator>()
                .Setup(x => x.GenerateSlug(It.IsAny<string[]>()))
                .Returns("slug");

            var sut = Mocker.CreateInstance<GroupBySeasonDocumentProcessor>();

            var documentJson = await LoadJsonTestData("GroupBySeason.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.TeamBySeason)
                .With(x => x.Season, 2024)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var group = await base.FootballDataContext.Groups
                .Include(g => g.Seasons)
                .AsNoTracking()
                .ToListAsync();

            group.Count.Should().Be(1);
            group.First().Seasons.Count.Should().Be(1);
        }

        [Fact]
        public async Task WhenGroupExistsButSeasonDoesNot_GroupSeasonIsCreated()
        {
            // arrange
            Mocker.GetMock<ISlugGenerator>()
                .Setup(x => x.GenerateSlug(It.IsAny<string[]>()))
                .Returns("slug");

            var sut = Mocker.CreateInstance<GroupBySeasonDocumentProcessor>();

            var documentJson = await LoadJsonTestData("GroupBySeason.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.TeamBySeason)
                .With(x => x.Season, 2024)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var group = await base.FootballDataContext.Groups
                .Include(g => g.Seasons)
                .AsNoTracking()
                .ToListAsync();

            group.Count.Should().Be(1);
            group.First().Seasons.Count.Should().Be(1);
        }
    }
}
