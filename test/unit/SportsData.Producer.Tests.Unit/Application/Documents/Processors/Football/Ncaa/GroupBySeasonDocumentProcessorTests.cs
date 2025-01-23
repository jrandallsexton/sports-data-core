using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Football.Ncaa.Espn;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Football.Ncaa
{
    public class GroupBySeasonDocumentProcessorTests : UnitTestBase<GroupBySeasonDocumentProcessor>
    {
        [Fact]
        public async Task WhenNeitherGroupNorSeasonExist_BothAreCreated()
        {
            // arrange
            var sut = Mocker.CreateInstance<GroupBySeasonDocumentProcessor>();

            var documentJson = await base.LoadJsonTestData("GroupBySeason.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.TeamBySeason)
                .With(x => x.Season, 2024)
                .With(x => x.Document, documentJson)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var group = await base.DataContext.Groups
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
            var sut = Mocker.CreateInstance<GroupBySeasonDocumentProcessor>();

            var documentJson = await base.LoadJsonTestData("GroupBySeason.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.TeamBySeason)
                .With(x => x.Season, 2024)
                .With(x => x.Document, documentJson)
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var group = await base.DataContext.Groups
                .Include(g => g.Seasons)
                .AsNoTracking()
                .ToListAsync();

            group.Count.Should().Be(1);
            group.First().Seasons.Count.Should().Be(1);
        }
    }
}
