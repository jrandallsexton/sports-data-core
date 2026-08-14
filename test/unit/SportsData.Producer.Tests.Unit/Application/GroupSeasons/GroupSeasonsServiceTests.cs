using FluentAssertions;

using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.GroupSeasons;

public class GroupSeasonsServiceTests : ProducerTestBase<GroupSeasonsService>
{
    /// <summary>
    /// Slug conventions differ by hierarchy vintage: 2025+ uses
    /// "fbs-i-a"; the backfilled pre-2025 hierarchies use "fbs". The
    /// service must resolve the FBS root (and its descendants) for both —
    /// the 2024 recompute campaign 500'd on the older vintage.
    /// </summary>
    [Theory]
    [InlineData("fbs-i-a")]
    [InlineData("fbs")]
    public async Task GetFbsGroupSeasonIds_ResolvesRootAcrossSlugVintages(string rootSlug)
    {
        var seasonYear = 2024;
        var root = NewGroup(seasonYear, rootSlug, parentId: null);
        var conference = NewGroup(seasonYear, "acc", root.Id);
        var independents = NewGroup(seasonYear, "fbs-indep", root.Id);
        var fcs = NewGroup(seasonYear, "fcs", parentId: null);

        await FootballDataContext.GroupSeasons.AddRangeAsync(root, conference, independents, fcs);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<GroupSeasonsService>();
        var ids = await sut.GetFbsGroupSeasonIds(seasonYear);

        ids.Should().BeEquivalentTo(
            [root.Id, conference.Id, independents.Id],
            "the FBS root and every descendant qualify; the FCS tree does not");
    }

    private static GroupSeason NewGroup(int seasonYear, string slug, Guid? parentId) => new()
    {
        Id = Guid.NewGuid(),
        SeasonYear = seasonYear,
        Slug = slug,
        Name = slug,
        Abbreviation = slug,
        ParentId = parentId
    };
}
