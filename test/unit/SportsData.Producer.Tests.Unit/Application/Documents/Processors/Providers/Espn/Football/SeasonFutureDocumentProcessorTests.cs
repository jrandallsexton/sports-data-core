using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class SeasonFutureDocumentProcessorTests : ProducerTestBase<SeasonFutureDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenEntityDoesNotExist_IsAdded()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<SeasonFutureDocumentProcessor<FootballDataContext>>();
        var season = new Season
        {
            Id = Guid.NewGuid(),
            Year = 2025,
            Name = "2025 NCAA Football",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 1, 15)
        };
        await FootballDataContext.Seasons.AddAsync(season);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaSeasonFuture.json");
        const string url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/futures/2758?lang=en";
        var urlHash = url.UrlHash();
        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.SeasonFuture)
            .With(x => x.Document, documentJson)
            .With(x => x.Season, season.Year)
            .With(x => x.UrlHash, urlHash)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var future = await FootballDataContext.SeasonFutures
            .Include(x => x.ExternalIds)
            .Include(x => x.Items)
            .FirstOrDefaultAsync();
        future.Should().NotBeNull();
        future!.Name.Should().Be("NCAA(F) - Championship");
        future.ExternalIds.Should().ContainSingle(x => x.Value == urlHash);
    }

    [Fact]
    public async Task WhenFranchiseSeasonsExist_BooksAreResolvedAndAdded()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<SeasonFutureDocumentProcessor<FootballDataContext>>();

        var season = Fixture.Build<Season>()
            .With(s => s.Id, Guid.NewGuid())
            .With(s => s.Year, 2025)
            .With(s => s.StartDate, new DateTime(2025, 8, 1))
            .With(s => s.EndDate, new DateTime(2026, 1, 15))
            .Create();

        await FootballDataContext.Seasons.AddAsync(season);

        // Seed FranchiseSeasons with ExternalIds matching known team $refs
        var teamRef1 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/194?lang=en");
        var teamRef2 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/251?lang=en");

        var hash1 = HashProvider.GenerateHashFromUri(teamRef1);
        var hash2 = HashProvider.GenerateHashFromUri(teamRef2);

        var franchiseSeason1 = Fixture.Build<FranchiseSeason>()
            .With(fs => fs.Id, Guid.NewGuid())
            //.With(fs => fs.SeasonId, season.Id)
            .With(fs => fs.SeasonYear, season.Year)
            .With(fs => fs.ExternalIds, new List<FranchiseSeasonExternalId>
            {
            new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = hash1,
                SourceUrlHash = hash1,
                SourceUrl = teamRef1.ToCleanUrl()
            }
            })
            .Create();

        var franchiseSeason2 = Fixture.Build<FranchiseSeason>()
            .With(fs => fs.Id, Guid.NewGuid())
            //.With(fs => fs.SeasonId, season.Id)
            .With(fs => fs.SeasonYear, season.Year)
            .With(fs => fs.ExternalIds, new List<FranchiseSeasonExternalId>
            {
            new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = hash2,
                SourceUrlHash = hash2,
                SourceUrl = teamRef2.ToCleanUrl()
            }
            })
            .Create();

        await FootballDataContext.FranchiseSeasons.AddRangeAsync(franchiseSeason1, franchiseSeason2);
        await FootballDataContext.SaveChangesAsync();

        // Build the document command
        var documentJson = await LoadJsonTestData("EspnFootballNcaaSeasonFuture.json");
        const string url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/futures/2758?lang=en";
        var urlHash = url.UrlHash();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.SeasonFuture)
            .With(x => x.Document, documentJson)
            .With(x => x.Season, season.Year)
            .With(x => x.UrlHash, urlHash)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var future = await FootballDataContext.SeasonFutures
            .Include(x => x.Items)
                .ThenInclude(i => i.Books)
            .FirstOrDefaultAsync();

        future.Should().NotBeNull();
        future!.Items.Should().NotBeEmpty();

        // Check that at least one Item has Books
        future.Items.SelectMany(i => i.Books).Should().NotBeEmpty();

        // Check that the Books have the correct FranchiseSeasonIds
        future.Items.SelectMany(i => i.Books)
            .Should().OnlyContain(book =>
                book.FranchiseSeasonId == franchiseSeason1.Id || book.FranchiseSeasonId == franchiseSeason2.Id
            );
    }


}
