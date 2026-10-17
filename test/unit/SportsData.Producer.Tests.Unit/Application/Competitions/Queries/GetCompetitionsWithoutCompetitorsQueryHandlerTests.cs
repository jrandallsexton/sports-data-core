using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutCompetitors;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.Queries;

public class GetCompetitionsWithoutCompetitorsQueryHandlerTests : ProducerTestBase<GetCompetitionsWithoutCompetitorsQueryHandler>
{
    private static readonly DateTime Kickoff = new(2026, 10, 10, 19, 30, 0, DateTimeKind.Utc);

    private async Task<Guid> SeedCompetitionAsync(string name, int competitorCount, DateTime startDateUtc)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();

        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = contestId,
            CreatedBy = Guid.Empty,
            CreatedUtc = Kickoff.AddDays(-7),
            Sport = Sport.FootballNfl,
            SeasonYear = 2026,
            Name = name,
            ShortName = name,
            StartDateUtc = startDateUtc
        });
        FootballDataContext.Competitions.Add(new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            CreatedBy = Guid.Empty,
            CreatedUtc = Kickoff.AddDays(-7),
            Date = startDateUtc
        });
        for (var i = 0; i < competitorCount; i++)
        {
            FootballDataContext.CompetitionCompetitors.Add(new FootballCompetitionCompetitor
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = i % 2 == 0 ? "home" : "away"
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return competitionId;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOnlyCompetitionsWithoutExactlyTwoCompetitors_FewestFirst()
    {
        var three = await SeedCompetitionAsync("Three", 3, Kickoff);
        await SeedCompetitionAsync("Two", 2, Kickoff);
        var one = await SeedCompetitionAsync("One", 1, Kickoff);
        var none = await SeedCompetitionAsync("None", 0, Kickoff);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery());

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.CompetitionId).Should().Equal(none, one, three);
        result.Value.Select(x => x.CompetitorCount).Should().Equal(0, 1, 3);
    }

    [Fact]
    public async Task ExecuteAsync_StampsTheContestsSport_AndStartDate()
    {
        var competitionId = await SeedCompetitionAsync("Bears at Packers", 0, Kickoff);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutCompetitorsQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutCompetitorsQuery());

        var row = result.Value.Should().ContainSingle().Subject;
        row.CompetitionId.Should().Be(competitionId);
        row.Sport.Should().Be(Sport.FootballNfl);
        row.StartDateUtc.Should().Be(Kickoff, "the embedded SQL this replaces never selected it");
        row.CompetitionName.Should().Be("Bears at Packers");
    }
}
