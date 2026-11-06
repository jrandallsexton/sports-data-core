#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Conference membership from group member lists: a TeamSeason document
/// delivered with a GroupSeason ParentId (GroupSeasonDocumentProcessor's
/// `teams` fan-out) sets GroupSeasonId, overriding the document's own
/// `groups` link, which ESPN renders as the CURRENT conference on historical
/// seasons.
/// </summary>
public class TeamSeasonDocumentProcessorMembershipTests :
    ProducerTestBase<TeamSeasonDocumentProcessor<FootballDataContext>>
{
    private const string LeagueBase = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football";

    private static readonly DateTime CreatedUtc = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly ExternalRefIdentityGenerator _generator = new();

    public TeamSeasonDocumentProcessorMembershipTests()
    {
        Mocker.Use<IGenerateExternalRefIdentities>(_generator);
    }

    [Fact]
    public async Task WhenDeliveredByMemberList_ExistingSeasonInWrongConference_IsCorrected()
    {
        var (json, dto) = await LoadTeamSeasonAsync();
        var fbs = await AddGroupAsync(2025, 1, 80, "fbs", parentId: null);
        var big12 = await AddGroupAsync(2025, 1, 4, "big12", fbs.Id);
        var mwest = await AddGroupAsync(2025, 1, 17, "mwest", fbs.Id);
        var franchiseSeason = await AddFranchiseSeasonAsync(dto, big12.Id, "fbs|big12");

        var published = CapturePublishedDocumentRequests();
        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();

        await sut.ProcessAsync(MemberListCommand(json, mwest.Id));

        var reloaded = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync(fs => fs.Id == franchiseSeason.Id);
        reloaded.GroupSeasonId.Should().Be(mwest.Id);
        reloaded.GroupSeasonMap.Should().Be("fbs|mwest");

        // Document-only: correcting membership must not re-spawn the team's children.
        published.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenDeliveredByAncestorMemberList_DivisionAssignment_IsKept()
    {
        var (json, dto) = await LoadTeamSeasonAsync();
        var fbs = await AddGroupAsync(2025, 1, 80, "fbs", parentId: null);
        var conference = await AddGroupAsync(2025, 1, 9, "pac12", fbs.Id);
        var division = await AddGroupAsync(2025, 1, 55, "pac12Sou", conference.Id);
        var franchiseSeason = await AddFranchiseSeasonAsync(dto, division.Id, "fbs|pac12|pac12Sou");

        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();

        await sut.ProcessAsync(MemberListCommand(json, conference.Id));
        await sut.ProcessAsync(MemberListCommand(json, fbs.Id));

        var reloaded = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync(fs => fs.Id == franchiseSeason.Id);
        reloaded.GroupSeasonId.Should().Be(division.Id, "a conference's list must not replace the division its children assigned");
        reloaded.GroupSeasonMap.Should().Be("fbs|pac12|pac12Sou");
    }

    [Fact]
    public async Task WhenNotDeliveredByMemberList_ExistingGroup_IsNotOverwrittenByDocumentLink()
    {
        var (json, dto) = await LoadTeamSeasonAsync();
        var fbs = await AddGroupAsync(2025, 1, 80, "fbs", parentId: null);
        var mwest = await AddGroupAsync(2025, 1, 17, "mwest", fbs.Id);
        // The fixture's own `groups` link points at groups/8.
        await AddGroupAsync(2025, 1, 8, "sec", fbs.Id);
        var franchiseSeason = await AddFranchiseSeasonAsync(dto, mwest.Id, groupSeasonMap: null);

        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();

        await sut.ProcessAsync(Command(json, parentId: null, includeLinkedDocumentTypes: []));

        var reloaded = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync(fs => fs.Id == franchiseSeason.Id);
        reloaded.GroupSeasonId.Should().Be(mwest.Id);
        reloaded.GroupSeasonMap.Should().Be("fbs|mwest", "a missing map is filled from the current group");
    }

    [Fact]
    public async Task WhenMemberListComesFromAnotherSeasonTypeVariant_TheVariantInUse_IsKept()
    {
        var (json, dto) = await LoadTeamSeasonAsync();
        var fbs3 = await AddGroupAsync(2025, 3, 80, "fbs", parentId: null);
        var big12Type3 = await AddGroupAsync(2025, 3, 4, "big12", fbs3.Id);
        var mwestType3 = await AddGroupAsync(2025, 3, 17, "mwest", fbs3.Id);
        var fbs2 = await AddGroupAsync(2025, 2, 80, "fbs", parentId: null);
        var mwestType2 = await AddGroupAsync(2025, 2, 17, "mwest", fbs2.Id);
        var franchiseSeason = await AddFranchiseSeasonAsync(dto, big12Type3.Id, "fbs|big12");

        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();

        await sut.ProcessAsync(MemberListCommand(json, mwestType2.Id));

        var reloaded = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync(fs => fs.Id == franchiseSeason.Id);
        reloaded.GroupSeasonId.Should().Be(mwestType3.Id,
            "conference W/L compares GroupSeasonId, so the season's types/3 variant must be kept");
    }

    [Fact]
    public async Task WhenDeliveredByMemberList_NewSeason_IsCreatedInTheListedGroup()
    {
        var (json, dto) = await LoadTeamSeasonAsync();
        var fbs = await AddGroupAsync(2025, 1, 80, "fbs", parentId: null);
        var mwest = await AddGroupAsync(2025, 1, 17, "mwest", fbs.Id);
        // No group for the document's own link (groups/8): the member list
        // must not depend on it, or it would request it and retry.
        await AddFranchiseAsync(dto);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();

        await sut.ProcessAsync(MemberListCommand(json, mwest.Id));

        var created = await FootballDataContext.FranchiseSeasons.AsNoTracking().SingleAsync();
        created.Id.Should().Be(_generator.Generate(dto.Ref).CanonicalId);
        created.GroupSeasonId.Should().Be(mwest.Id);
        created.GroupSeasonMap.Should().Be("fbs|mwest");

        bus.Verify(x => x.Publish(It.IsAny<FranchiseSeasonCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private async Task<(string Json, EspnTeamSeasonDto Dto)> LoadTeamSeasonAsync()
    {
        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeason.json");
        return (json, json.FromJson<EspnTeamSeasonDto>()!);
    }

    private async Task<GroupSeason> AddGroupAsync(
        int seasonYear,
        int seasonType,
        int espnGroupId,
        string abbreviation,
        Guid? parentId)
    {
        var identity = _generator.Generate(new Uri($"{LeagueBase}/seasons/{seasonYear}/types/{seasonType}/groups/{espnGroupId}"));

        var group = new GroupSeason
        {
            Id = identity.CanonicalId,
            ParentId = parentId,
            SeasonYear = seasonYear,
            Name = abbreviation,
            Slug = abbreviation,
            Abbreviation = abbreviation,
            CreatedBy = Guid.Empty,
            CreatedUtc = CreatedUtc,
            ExternalIds =
            [
                new GroupSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    GroupSeasonId = identity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    CreatedBy = Guid.Empty,
                    CreatedUtc = CreatedUtc
                }
            ]
        };

        await FootballDataContext.GroupSeasons.AddAsync(group);
        await FootballDataContext.SaveChangesAsync();
        return group;
    }

    private async Task<Franchise> AddFranchiseAsync(EspnTeamSeasonDto dto, List<FranchiseSeason>? seasons = null)
    {
        var franchiseIdentity = _generator.Generate(dto.Franchise.Ref);

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseIdentity.CanonicalId)
            .With(x => x.Name, "Test Franchise")
            .With(x => x.ExternalIds, new List<FranchiseExternalId>
            {
                Fixture.Build<FranchiseExternalId>()
                    .WithAutoProperties()
                    .With(x => x.Provider, SourceDataProvider.Espn)
                    .With(x => x.SourceUrl, franchiseIdentity.CleanUrl)
                    .With(x => x.SourceUrlHash, franchiseIdentity.UrlHash)
                    .With(x => x.Value, franchiseIdentity.UrlHash)
                    .Create()
            })
            .With(x => x.Seasons, seasons ?? new List<FranchiseSeason>())
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.SaveChangesAsync();
        return franchise;
    }

    private async Task<FranchiseSeason> AddFranchiseSeasonAsync(
        EspnTeamSeasonDto dto,
        Guid groupSeasonId,
        string? groupSeasonMap)
    {
        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, _generator.Generate(dto.Ref).CanonicalId)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.Abbreviation, "TST")
            .With(x => x.ColorCodeHex, "000000")
            .With(x => x.DisplayName, "Test")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test")
            .With(x => x.Name, "Test")
            .With(x => x.Slug, "test")
            .With(x => x.GroupSeasonId, groupSeasonId)
            .With(x => x.GroupSeasonMap, groupSeasonMap)
            .Create();

        await AddFranchiseAsync(dto, [franchiseSeason]);
        return franchiseSeason;
    }

    private List<DocumentRequested> CapturePublishedDocumentRequests()
    {
        var published = new List<DocumentRequested>();
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .Callback<DocumentRequested, CancellationToken>((evt, _) => published.Add(evt))
            .Returns(Task.CompletedTask);
        return published;
    }

    // The fan-out's command: document-only, parent = the listing group.
    private ProcessDocumentCommand MemberListCommand(string json, Guid groupSeasonId) =>
        Command(json, groupSeasonId.ToString(), includeLinkedDocumentTypes: []);

    private ProcessDocumentCommand Command(
        string json,
        string? parentId,
        IReadOnlyCollection<DocumentType>? includeLinkedDocumentTypes) =>
        Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.Document, json)
            .With(x => x.ParentId, parentId)
            .With(x => x.IncludeLinkedDocumentTypes, includeLinkedDocumentTypes)
            .OmitAutoProperties()
            .Create();
}
