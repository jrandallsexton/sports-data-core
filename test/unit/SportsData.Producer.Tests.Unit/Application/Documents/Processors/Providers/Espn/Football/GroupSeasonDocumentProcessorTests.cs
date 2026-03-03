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
using SportsData.Producer.Infrastructure.Data.Football;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class GroupSeasonDocumentProcessorTests : ProducerTestBase<GroupSeasonDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenGroupAndSeasonDoNotExist_Sec2024_IsCreated()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor<FootballDataContext>>();

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
            .With(x => x.SeasonYear, 2024)
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

    /// <summary>
    /// Regression test for the duplicate GroupSeason SeasonYear bug.
    ///
    /// ESPN's child $ref links can point to an older season's URL even when the parent command
    /// carries a newer season year (e.g. a 2023 parent spawns a child ref with seasons/2016 in the URL).
    /// The processor must derive SeasonYear from the document's own $ref, NOT from command.Season.
    /// </summary>
    [Fact]
    public async Task WhenRefUrlYearDiffersFromCommandSeason_SeasonYearIsSetFromUrl_NotFromCommand()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor<FootballDataContext>>();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2024.json");
        var dto = documentJson.FromJson<EspnGroupSeasonDto>();
        dto!.Parent = null;

        // Simulate the bug: override the $ref to reference seasons/2016 even though command.Season=2023
        dto.Ref = new Uri(dto.Ref.ToString().Replace("/seasons/2024/", "/seasons/2016/"));

        var seasonIdentity = generator.Generate(dto.Season.Ref);
        var season = Fixture.Build<Season>()
            .OmitAutoProperties()
            .With(x => x.Id, seasonIdentity.CanonicalId)
            .With(x => x.Year, 2023)
            .With(x => x.Name, "2023")
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
            .With(x => x.SeasonYear, 2023)   // parent season — intentionally different from URL year
            .With(x => x.Document, dto.ToJson())
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert — SeasonYear must come from the URL (2016), not from command.Season (2023)
        var group = await FootballDataContext.GroupSeasons.FirstOrDefaultAsync();
        group.Should().NotBeNull();
        group!.SeasonYear.Should().Be(2016,
            "SeasonYear must be extracted from the document $ref URL, not blindly inherited from command.Season");
    }

    [Fact(Skip = "Revisit")]
    public async Task WhenGroupExistsAndSeasonIsNew_Sec2025_IsAppendedToExistingGroup()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor<FootballDataContext>>();

        var json2024 = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2024.json");
        var json2025 = await LoadJsonTestData("EspnFootballNcaaGroupSeason_Sec2025.json");

        var cmd2024 = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.Document, json2024)
            .OmitAutoProperties()
            .Create();

        var cmd2025 = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.SeasonYear, 2025)
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
