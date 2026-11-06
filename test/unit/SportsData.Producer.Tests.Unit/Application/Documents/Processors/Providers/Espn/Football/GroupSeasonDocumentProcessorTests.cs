#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
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

        var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaGroupSeason_Sec2024.json");
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

        var bus = Mocker.GetMock<IEventBus>();

        // act
        await sut.ProcessAsync(command);

        // assert
        var group = await FootballDataContext.GroupSeasons.FirstOrDefaultAsync();
        group.Should().NotBeNull();
        group.SeasonYear.Should().Be(2024);

        // The SEC logo (dto.Logos) must be published as a GroupSeasonLogo image
        // request — previously dropped, leaving the GroupSeasonLogo table empty.
        // The fixture carries exactly one logo; assert count, parent, type, and
        // season year on every request.
        bus.Verify(x => x.PublishBatch(
            It.Is<IEnumerable<ProcessImageRequest>>(reqs =>
                reqs.Count() == 1
                && reqs.All(r => r.ParentEntityId == group.Id
                                 && r.DocumentType == DocumentType.GroupSeasonLogo
                                 && r.SeasonYear == 2024)),
            It.IsAny<CancellationToken>()),
            Times.Once);
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

        var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaGroupSeason_Sec2024.json");
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

        var bus = Mocker.GetMock<IEventBus>();

        // act
        await sut.ProcessAsync(command);

        // assert — SeasonYear must come from the URL (2016), not from command.Season (2023)
        var group = await FootballDataContext.GroupSeasons.FirstOrDefaultAsync();
        group.Should().NotBeNull();
        group!.SeasonYear.Should().Be(2016,
            "SeasonYear must be extracted from the document $ref URL, not blindly inherited from command.Season");

        // The logo image request must carry the same URL-derived season year
        // (2016), not command.Season (2023) — proving ProcessLogos uses the year
        // the processor resolved from the $ref, not the parent command's year.
        bus.Verify(x => x.PublishBatch(
            It.Is<IEnumerable<ProcessImageRequest>>(reqs =>
                reqs.Count() == 1
                && reqs.All(r => r.ParentEntityId == group.Id
                                 && r.DocumentType == DocumentType.GroupSeasonLogo
                                 && r.SeasonYear == 2016)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(Skip = "Revisit")]
    public async Task WhenGroupExistsAndSeasonIsNew_Sec2025_IsAppendedToExistingGroup()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor<FootballDataContext>>();

        var json2024 = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaGroupSeason_Sec2024.json");
        var json2025 = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaGroupSeason_Sec2025.json");

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

    // ---- Member lists: authoritative conference membership ----

    [Fact]
    public async Task WhenNewLeafGroup_PublishesItsMemberListAsDocumentOnlyTeamSeasonRequest()
    {
        var (sut, dto) = await ArrangeSec2024Async();
        var published = CaptureDocumentRequests();

        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));

        var group = await FootballDataContext.GroupSeasons.SingleAsync();
        var members = published.Where(r => r.DocumentType == DocumentType.TeamSeason).ToList();

        members.Should().ContainSingle();
        members[0].ParentId.Should().Be(group.Id.ToString());
        members[0].Uri.AbsolutePath.Should().EndWith("/groups/8/teams");
        members[0].IncludeLinkedDocumentTypes.Should().NotBeNull().And.BeEmpty(
            "the fan-out corrects membership only; it must not cascade every team's children");
    }

    [Fact]
    public async Task WhenNewGroupHasChildren_PublishesChildrenAndNotItsMemberList()
    {
        var (sut, dto) = await ArrangeSec2024Async();
        dto.Children = new EspnLinkDto { Ref = new Uri(dto.Ref.ToString().Replace("?lang=en", "/children?lang=en")) };
        var published = CaptureDocumentRequests();

        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));

        published.Should().ContainSingle(r => r.DocumentType == DocumentType.GroupSeason);
        published.Should().NotContain(r => r.DocumentType == DocumentType.TeamSeason,
            "a conference with divisions is not a leaf; its divisions' lists are more specific");
    }

    [Fact]
    public async Task WhenExistingGroupAndNoFilter_PublishesNothing()
    {
        var (sut, dto) = await ArrangeSec2024Async();
        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));
        var published = CaptureDocumentRequests();

        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));

        published.Should().BeEmpty("an ordinary re-hit of an existing group must not re-walk its member list");
    }

    [Fact]
    public async Task WhenExistingGroupAndMembershipRequested_PublishesItsMemberList()
    {
        var (sut, dto) = await ArrangeSec2024Async();
        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));
        var published = CaptureDocumentRequests();

        await sut.ProcessAsync(Command(dto, [DocumentType.GroupSeason, DocumentType.TeamSeason]));

        published.Should().ContainSingle(r => r.DocumentType == DocumentType.TeamSeason);
    }

    [Fact]
    public async Task WhenExistingFbsGroupHasUnknownAbbreviation_ItIsResolvedAndMapsBelowAreRefreshed()
    {
        var (sut, dto) = await ArrangeSec2024Async();
        var generator = new ExternalRefIdentityGenerator();

        // Re-point the fixture at the FBS node (ESPN group 80), which ESPN
        // serves without an abbreviation.
        dto.Ref = new Uri(dto.Ref.ToString().Replace("/groups/8?", "/groups/80?"));
        dto.Id = 80;
        dto.Abbreviation = null!;
        dto.Teams = null!;

        var fbsIdentity = generator.Generate(dto.Ref);
        var fbs = new GroupSeason
        {
            Id = fbsIdentity.CanonicalId,
            SeasonYear = 2024,
            Name = "FBS",
            Slug = "fbs",
            Abbreviation = "UNK",
            CreatedBy = Guid.Empty,
            CreatedUtc = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)
        };
        var sec = new GroupSeason
        {
            Id = Guid.NewGuid(),
            ParentId = fbs.Id,
            SeasonYear = 2024,
            Name = "SEC",
            Slug = "sec",
            Abbreviation = "sec",
            CreatedBy = Guid.Empty,
            CreatedUtc = new DateTime(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc)
        };
        await FootballDataContext.GroupSeasons.AddRangeAsync(fbs, sec);

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.Abbreviation, "TST")
            .With(x => x.ColorCodeHex, "000000")
            .With(x => x.DisplayName, "Test")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test")
            .With(x => x.Slug, "test")
            .With(x => x.GroupSeasonId, sec.Id)
            .With(x => x.GroupSeasonMap, "UNK|sec")
            .Create();
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        await sut.ProcessAsync(Command(dto, includeLinkedDocumentTypes: null));

        var reloadedFbs = await FootballDataContext.GroupSeasons.AsNoTracking().SingleAsync(g => g.Id == fbs.Id);
        reloadedFbs.Abbreviation.Should().Be("fbs");

        var reloaded = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync(fs => fs.Id == franchiseSeason.Id);
        reloaded.GroupSeasonMap.Should().Be("fbs|sec", "every '|fbs' filter depends on the resolved abbreviation");
    }

    private async Task<(GroupSeasonDocumentProcessor<FootballDataContext> Sut, EspnGroupSeasonDto Dto)> ArrangeSec2024Async()
    {
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<GroupSeasonDocumentProcessor<FootballDataContext>>();

        var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaGroupSeason_Sec2024.json");
        var dto = documentJson.FromJson<EspnGroupSeasonDto>()!;
        dto.Parent = null;
        dto.Season = null!;

        return (sut, dto);
    }

    private List<DocumentRequested> CaptureDocumentRequests()
    {
        var published = new List<DocumentRequested>();
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentRequested, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);
        return published;
    }

    private ProcessDocumentCommand Command(
        EspnGroupSeasonDto dto,
        IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.GroupSeason)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.Document, dto.ToJson())
            .With(x => x.IncludeLinkedDocumentTypes, includeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();
}
