using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.GroupSeasons.Queries.GetFbsGroupSeasonIds;
using SportsData.Producer.Infrastructure.Data.Entities;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.GroupSeasons.Queries.GetFbsGroupSeasonIds;

public class GetFbsGroupSeasonIdsQueryHandlerTests : ProducerTestBase<GetFbsGroupSeasonIdsQueryHandler>
{
    /// <summary>
    /// Slug conventions differ by hierarchy vintage: 2025+ uses
    /// "fbs-i-a"; the backfilled pre-2025 hierarchies use "fbs". The
    /// handler must resolve the FBS root (and its descendants) for both —
    /// the 2024 recompute campaign 500'd on the older vintage.
    /// </summary>
    [Theory]
    [InlineData("fbs-i-a")]
    [InlineData("fbs")]
    public async Task ExecuteAsync_ResolvesRootAcrossSlugVintages(string rootSlug)
    {
        var seasonYear = 2024;
        var root = NewGroup(seasonYear, rootSlug, parentId: null);
        var conference = NewGroup(seasonYear, "acc", root.Id);
        var independents = NewGroup(seasonYear, "fbs-indep", root.Id);
        var fcs = NewGroup(seasonYear, "fcs", parentId: null);

        await FootballDataContext.GroupSeasons.AddRangeAsync(root, conference, independents, fcs);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<GetFbsGroupSeasonIdsQueryHandler>();
        var result = await sut.ExecuteAsync(new GetFbsGroupSeasonIdsQuery(seasonYear));

        result.Should().BeOfType<Success<HashSet<Guid>>>();
        result.Value.Should().BeEquivalentTo(
            [root.Id, conference.Id, independents.Id],
            "the FBS root and every descendant qualify; the FCS tree does not");
    }

    [Fact]
    public async Task ExecuteAsync_NoFbsRootForSeason_ReturnsNotFound()
    {
        var fcs = NewGroup(2024, "fcs", parentId: null);
        var otherYearRoot = NewGroup(2025, "fbs-i-a", parentId: null);

        await FootballDataContext.GroupSeasons.AddRangeAsync(fcs, otherYearRoot);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<GetFbsGroupSeasonIdsQueryHandler>();
        var result = await sut.ExecuteAsync(new GetFbsGroupSeasonIdsQuery(2024));

        result.Should().BeOfType<Failure<HashSet<Guid>>>();
        result.Status.Should().Be(ResultStatus.NotFound);
        ((Failure<HashSet<Guid>>)result).Errors.Should().ContainSingle();
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
