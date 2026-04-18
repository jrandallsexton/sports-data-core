using System.Net;
using System.Net.Http.Json;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Tests.Integration.Fixtures;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Integration.Leagues;

[Collection(nameof(ApiIntegrationCollection))]
public sealed class CreateFootballNcaaLeagueTests
{
    private readonly ApiIntegrationFixture _fixture;

    public CreateFootballNcaaLeagueTests(ApiIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Post_ValidPayload_CreatesLeagueAndMembership()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        _fixture.Factory.FranchiseClientFactory.ResolveSlugsAsNewGuids();

        var client = _fixture.Factory.CreateClient();

        var request = new CreateFootballNcaaLeagueRequest
        {
            Name = "Integration Test NCAA League",
            Description = "Seeded by integration test",
            PickType = "StraightUp",
            TiebreakerType = "TotalPoints",
            TiebreakerTiePolicy = "EarliestSubmission",
            RankingFilter = "AP_TOP_25",
            ConferenceSlugs = ["sec", "big-ten"],
            UseConfidencePoints = false,
            IsPublic = true,
            DropLowWeeksCount = 0,
            SeasonYear = DateTime.UtcNow.Year,
        };

        // Act
        var response = await client.PostAsJsonAsync(
            "/ui/leagues/football/ncaa",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();

        var league = await db.PickemGroups
            .Include(g => g.Conferences)
            .Include(g => g.Members)
            .SingleAsync(g => g.Name == request.Name);

        league.Sport.Should().Be(Sport.FootballNcaa);
        league.League.Should().Be(League.NCAAF);
        league.PickType.Should().Be(PickType.StraightUp);
        league.RankingFilter.Should().Be(TeamRankingFilter.AP_TOP_25);
        league.Conferences.Should().HaveCount(2);
        league.Conferences.Select(c => c.ConferenceSlug)
            .Should().BeEquivalentTo(new[] { "sec", "big-ten" });
        league.Members.Should().ContainSingle(m => m.UserId == _fixture.TestUserId
            && m.Role == LeagueRole.Commissioner);
        league.CommissionerUserId.Should().Be(_fixture.TestUserId);
    }

    [Fact]
    public async Task Post_UnknownConferenceSlug_ReturnsValidationFailure()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        _fixture.Factory.FranchiseClientFactory.Client
            .Setup(x => x.GetConferenceIdsBySlugs(
                It.IsAny<int>(),
                It.IsAny<List<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, List<string> slugs, CancellationToken _) =>
                slugs
                    .Where(s => s != "nope")
                    .ToDictionary(_ => Guid.NewGuid(), s => s));

        var client = _fixture.Factory.CreateClient();

        var request = new CreateFootballNcaaLeagueRequest
        {
            Name = "Bad Slug League",
            PickType = "StraightUp",
            TiebreakerType = "TotalPoints",
            TiebreakerTiePolicy = "EarliestSubmission",
            ConferenceSlugs = ["sec", "nope"],
        };

        // Act
        var response = await client.PostAsJsonAsync(
            "/ui/leagues/football/ncaa",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var scope = _fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
        var rows = await db.PickemGroups.CountAsync(g => g.Name == request.Name);
        rows.Should().Be(0);
    }
}
