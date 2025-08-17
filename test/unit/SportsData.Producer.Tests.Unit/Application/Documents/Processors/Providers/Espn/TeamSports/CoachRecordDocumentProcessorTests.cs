using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class CoachRecordDocumentProcessorTests : ProducerTestBase<CoachRecordDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task WhenCoachDoesNotExist_ShouldDoNothing()
    {
        // Arrange
        var url = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872?lang=en&region=us";
        var urlHash = HashProvider.UrlHash(url);
        var sut = Mocker.CreateInstance<CoachRecordDocumentProcessor<TeamSportDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaCoachRecord.json");
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
        var coach = await TeamSportDataContext.Coaches
            .Include(x => x.Records)
            .FirstOrDefaultAsync();
        coach.Should().BeNull("coach must exist before records can be processed");
    }

    [Fact]
    public async Task WhenCoachExists_ShouldCreateRecords()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var coachIdentity =
            generator.Generate(
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872?lang=en&region=us");

        var coach = new Coach
        {
            Id = coachIdentity.CanonicalId,
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 25,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Fixture.Create<Guid>(),
            ExternalIds =
            [
                new CoachExternalId
                {
                    Id = Fixture.Create<Guid>(),
                    Provider = SourceDataProvider.Espn,
                    Value = coachIdentity.UrlHash,
                    SourceUrlHash = coachIdentity.UrlHash,
                    SourceUrl = coachIdentity.CleanUrl
                }
            ]
        };
        await TeamSportDataContext.Coaches.AddAsync(coach);
        await TeamSportDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<CoachRecordDocumentProcessor<TeamSportDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaCoachRecord.json");
        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Coach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872/record/2?lang=en&region=us".UrlHash())
            .With(x => x.ParentId, coach.Id.ToString)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var updated = await TeamSportDataContext.Coaches
            .Include(x => x.Records)
            .ThenInclude(r => r.Stats)
            .FirstOrDefaultAsync(x => x.Id == coach.Id);

        updated.Should().NotBeNull();
        updated!.Records.Should().NotBeEmpty();
        updated.Records.SelectMany(r => r.Stats).Should().NotBeEmpty();
    }
}
