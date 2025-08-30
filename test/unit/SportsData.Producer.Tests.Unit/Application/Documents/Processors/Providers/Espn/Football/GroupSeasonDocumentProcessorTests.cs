using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class GroupSeasonDocumentProcessorTests : ProducerTestBase<GroupSeasonDocumentProcessor>
{
    [Fact]
    public async Task WhenGroupAndSeasonDoNotExist_Sec2024_IsCreated()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor>();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2024.json");
        var dto = documentJson.FromJson<EspnGroupSeasonDto>();
        dto!.Parent = null;

        var seasonIdentity = generator.Generate(dto!.Season.Ref);
        var season = Fixture.Build<Season>()
            .OmitAutoProperties()
            .With(x => x.Id, seasonIdentity.CanonicalId)
            .With(x => x.Year, 2025)
            .With(x => x.Name, "2025")
            .With(x => x.ExternalIds, new List<SeasonExternalId>()
            {
                new SeasonExternalId()
                {
                    Id = Guid.NewGuid(),
                    SourceUrl = seasonIdentity.CleanUrl,
                    SourceUrlHash = seasonIdentity.UrlHash,
                    Value = seasonIdentity.CanonicalId.ToString(),
                    CreatedBy = Guid.Empty,
                    CreatedUtc = DateTime.UtcNow,
                    SeasonId = seasonIdentity.CanonicalId
                }
            })
            .Create();
        await FootballDataContext.Seasons.AddAsync(season);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.Season, 2024)
            .With(x => x.Document, dto.ToJson())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var group = await FootballDataContext.GroupSeasons.FirstOrDefaultAsync();
        group.Should().NotBeNull();
        group.SeasonYear.Should().Be(2024);
    }

    [Fact(Skip = "Revisit")]
    public async Task WhenGroupExistsAndSeasonIsNew_Sec2025_IsAppendedToExistingGroup()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor>();

        var json2024 = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2024.json");
        var json2025 = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2025.json");

        var cmd2024 = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.Season, 2024)
            .With(x => x.Document, json2024)
            .OmitAutoProperties()
            .Create();

        var cmd2025 = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.Season, 2025)
            .With(x => x.Document, json2025)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(cmd2024);
        await sut.ProcessAsync(cmd2025);

        // assert (fresh load!)
        var group = await FootballDataContext.GroupSeasons
            .Include(g => g.ExternalIds)
            .FirstOrDefaultAsync(g =>
                g.ExternalIds.Any(x => x.Provider == SourceDataProvider.Espn && x.Value == "8"));

        group.Should().NotBeNull("group with external ID '8' should have been created");
    }

}
