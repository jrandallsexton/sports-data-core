using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

[Collection("Sequential")]
public class TeamSeasonAwardDocumentProcessorTests : ProducerTestBase<TeamSeasonAwardDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task ProcessAsync_CreatesAward_WhenAwardDoesNotExist()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeason = await SeedFranchiseSeasonAsync(2019);

        var awardDto = new EspnAwardDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/3?lang=en"),
            Id = "3",
            Name = "Davey O'Brien Award",
            Description = "National Quarterback Award",
            History = "Presented to the nation's outstanding QB by the Davey O'Brien Foundation.",
            Winners = new List<EspnAwardDto.WinnerDto>()
        };

        var documentJson = awardDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonAward)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeason.Id.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonAwardDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var awards = await FootballDataContext.Awards.Include(x => x.ExternalIds).ToListAsync();
        awards.Should().HaveCount(1);

        var award = awards.First();
        award.Name.Should().Be("Davey O'Brien Award");
        award.Description.Should().Be("National Quarterback Award");
        award.History.Should().StartWith("Presented to the nation's outstanding QB");
        award.ExternalIds.Should().HaveCount(1);
        award.ExternalIds.First().Provider.Should().Be(SourceDataProvider.Espn);
    }

    [Fact]
    public async Task ProcessAsync_CreatesAwardWinners_FromDtoWinners()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeason = await SeedFranchiseSeasonAsync(2019);

        var awardDto = new EspnAwardDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/3?lang=en"),
            Id = "3",
            Name = "Davey O'Brien Award",
            Description = "National Quarterback Award",
            History = "Presented to the nation's outstanding QB.",
            Winners = new List<EspnAwardDto.WinnerDto>
            {
                new EspnAwardDto.WinnerDto
                {
                    Athlete = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/athletes/3915511?lang=en") },
                    Team = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/99?lang=en") }
                }
            }
        };

        var documentJson = awardDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonAward)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeason.Id.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonAwardDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var winners = await FootballDataContext.FranchiseSeasonAwardWinners.ToListAsync();
        winners.Should().HaveCount(1);

        var winner = winners.First();
        winner.AthleteRef.Should().Be("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/athletes/3915511?lang=en");
        winner.TeamRef.Should().Be("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/teams/99?lang=en");
    }

    [Fact]
    public async Task ProcessAsync_UsesCanonicalAwardUrl_ForAwardIdentity()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeason = await SeedFranchiseSeasonAsync(2019);

        var awardDto = new EspnAwardDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/awards/3?lang=en"),
            Id = "3",
            Name = "Davey O'Brien Award",
            Description = "National Quarterback Award",
            History = "Presented to the nation's outstanding QB.",
            Winners = new List<EspnAwardDto.WinnerDto>()
        };

        var documentJson = awardDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonAward)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeason.Id.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonAwardDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var award = await FootballDataContext.Awards.Include(x => x.ExternalIds).FirstAsync();
        var externalId = award.ExternalIds.First();

        // Should use canonical URL (without seasons segment)
        externalId.SourceUrl.Should().Be("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/awards/3");
    }

    private async Task<FranchiseSeason> SeedFranchiseSeasonAsync(int year)
    {
        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, year)
            .With(x => x.Slug, "lsu-tigers")
            .With(x => x.Location, "LSU")
            .With(x => x.Name, "Tigers")
            .With(x => x.Abbreviation, "LSU")
            .With(x => x.DisplayName, "LSU Tigers")
            .With(x => x.DisplayNameShort, "LSU")
            .With(x => x.ColorCodeHex, "#461D7C")
            .With(x => x.CreatedUtc, DateTime.UtcNow)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        return franchiseSeason;
    }
}
