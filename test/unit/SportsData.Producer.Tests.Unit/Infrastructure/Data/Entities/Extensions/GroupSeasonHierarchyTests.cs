#nullable enable

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Infrastructure.Data.Entities.Extensions;

public class GroupSeasonHierarchyTests : ProducerTestBase<GroupSeasonHierarchyTests>
{
    private const string NcaaBase = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football";
    private const string NflBase = "http://sports.core.api.espn.com/v2/sports/football/leagues/nfl";

    private static readonly DateTime CreatedUtc = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);

    private readonly ExternalRefIdentityGenerator _generator = new();

    [Theory]
    [InlineData(80, null, "fbs")]
    [InlineData(80, "UNK", "fbs")]
    [InlineData(81, "", "fcs")]
    [InlineData(8, "sec", "sec")]
    [InlineData(4, null, "UNK")]
    public void ResolveAbbreviation_NcaaFootball(long espnGroupId, string? espnAbbreviation, string expected)
    {
        var dto = new EspnGroupSeasonDto
        {
            Id = espnGroupId,
            Abbreviation = espnAbbreviation!,
            Ref = new Uri($"{NcaaBase}/seasons/2011/types/3/groups/{espnGroupId}?lang=en")
        };

        GroupSeasonHierarchy.ResolveAbbreviation(dto).Should().Be(expected);
    }

    [Fact]
    public void ResolveAbbreviation_OtherLeagues_DoNotUseTheNcaaDivisionIds()
    {
        var dto = new EspnGroupSeasonDto
        {
            Id = 80,
            Abbreviation = null!,
            Ref = new Uri($"{NflBase}/seasons/2011/types/2/groups/80")
        };

        GroupSeasonHierarchy.ResolveAbbreviation(dto).Should().Be(GroupSeasonHierarchy.UnknownAbbreviation);
    }

    [Fact]
    public async Task BuildMapAsync_JoinsAbbreviationsFromTheRootDown()
    {
        var root = await AddGroupAsync(3, 99, "NCAAF", null);
        var ncaa = await AddGroupAsync(3, 90, "NCAA", root.Id);
        var fbs = await AddGroupAsync(3, 80, "fbs", ncaa.Id);
        var sec = await AddGroupAsync(3, 8, "sec", fbs.Id);

        var map = await GroupSeasonHierarchy.BuildMapAsync(FootballDataContext, sec.Id);

        map.Should().Be("NCAAF|NCAA|fbs|sec");
    }

    [Fact]
    public async Task BuildMapAsync_UnknownGroup_ReturnsNull()
    {
        var map = await GroupSeasonHierarchy.BuildMapAsync(FootballDataContext, Guid.NewGuid());

        map.Should().BeNull();
    }

    [Fact]
    public async Task IsAncestorAsync_TrueForAnyStrictAncestor_FalseOtherwise()
    {
        var fbs = await AddGroupAsync(3, 80, "fbs", null);
        var pac12 = await AddGroupAsync(3, 9, "pac12", fbs.Id);
        var south = await AddGroupAsync(3, 55, "pac12Sou", pac12.Id);
        var mwest = await AddGroupAsync(3, 17, "mwest", fbs.Id);

        (await GroupSeasonHierarchy.IsAncestorAsync(FootballDataContext, pac12.Id, south.Id)).Should().BeTrue();
        (await GroupSeasonHierarchy.IsAncestorAsync(FootballDataContext, fbs.Id, south.Id)).Should().BeTrue();
        (await GroupSeasonHierarchy.IsAncestorAsync(FootballDataContext, south.Id, south.Id)).Should().BeFalse();
        (await GroupSeasonHierarchy.IsAncestorAsync(FootballDataContext, mwest.Id, south.Id)).Should().BeFalse();
        (await GroupSeasonHierarchy.IsAncestorAsync(FootballDataContext, south.Id, pac12.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task MatchVariantAsync_MapsToTheVariantTheCurrentGroupUses()
    {
        var big12Type3 = await AddGroupAsync(3, 4, "big12", null);
        var mwestType3 = await AddGroupAsync(3, 17, "mwest", null);
        var mwestType2 = await AddGroupAsync(2, 17, "mwest", null);

        var matched = await GroupSeasonHierarchy.MatchVariantAsync(
            FootballDataContext, _generator, mwestType2.Id, big12Type3.Id);

        matched.Should().Be(mwestType3.Id);
    }

    [Fact]
    public async Task MatchVariantAsync_NoCurrentGroup_OrVariantNotSourced_KeepsIncoming()
    {
        var big12Type3 = await AddGroupAsync(3, 4, "big12", null);
        var mwestType2 = await AddGroupAsync(2, 17, "mwest", null);

        (await GroupSeasonHierarchy.MatchVariantAsync(FootballDataContext, _generator, mwestType2.Id, null))
            .Should().Be(mwestType2.Id);

        (await GroupSeasonHierarchy.MatchVariantAsync(FootballDataContext, _generator, mwestType2.Id, big12Type3.Id))
            .Should().Be(mwestType2.Id, "the types/3 Mountain West row does not exist");
    }

    private async Task<GroupSeason> AddGroupAsync(int seasonType, int espnGroupId, string abbreviation, Guid? parentId)
    {
        var identity = _generator.Generate(new Uri($"{NcaaBase}/seasons/2005/types/{seasonType}/groups/{espnGroupId}"));

        var group = new GroupSeason
        {
            Id = identity.CanonicalId,
            ParentId = parentId,
            SeasonYear = 2005,
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
}
