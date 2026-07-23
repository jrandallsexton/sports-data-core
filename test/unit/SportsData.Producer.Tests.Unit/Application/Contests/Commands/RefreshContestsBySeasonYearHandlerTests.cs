#nullable enable

using AutoFixture;

using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Commands;

public class RefreshContestsBySeasonYearHandlerTests :
    ProducerTestBase<RefreshContestsBySeasonYearHandler>
{
    public RefreshContestsBySeasonYearHandlerTests()
    {
        // Fixed clock so the validator's "no more than one year in the future"
        // rule is deterministic (test seasons are 2024/2025).
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Mocker.Use<IValidator<RefreshContestsBySeasonYearCommand>>(
            new RefreshContestsBySeasonYearCommandValidator(clock.Object));
    }

    private FranchiseSeason NewFranchiseSeason(int seasonYear) =>
        Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Abbreviation, "AB")
            .With(x => x.ColorCodeHex, "#FFFFFF")
            .With(x => x.DisplayName, "Team")
            .With(x => x.DisplayNameShort, "T")
            .With(x => x.Location, "Loc")
            .With(x => x.Slug, $"team-{Guid.NewGuid():N}")
            .OmitAutoProperties()
            .Create();

    private FootballCompetition NewCompetition(Guid contestId) =>
        Fixture.Build<FootballCompetition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contestId)
            .OmitAutoProperties()
            .Create();

    private FootballCompetitionCompetitor NewCompetitor(Guid competitionId, Guid franchiseSeasonId, string homeAway) =>
        Fixture.Build<FootballCompetitionCompetitor>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.CompetitionId, competitionId)
            .With(x => x.FranchiseSeasonId, franchiseSeasonId)
            .With(x => x.HomeAway, homeAway)
            .OmitAutoProperties()
            .Create();

    [Fact]
    public async Task SharedContest_HomeAndAway_EnqueuesEachContestExactlyOnce_ExcludesOtherSeasons()
    {
        // arrange
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<RefreshContestsBySeasonYearHandler>();

        const int season = 2024;

        // Two franchise seasons that share ONE competition (home vs away).
        var teamA = NewFranchiseSeason(season);
        var teamB = NewFranchiseSeason(season);
        // A third franchise season in a DIFFERENT season — its contest must be excluded.
        var teamNextSeason = NewFranchiseSeason(season + 1);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(teamA, teamB, teamNextSeason);

        // One shared contest for A vs B in the target season.
        var sharedContestId = Guid.NewGuid();
        var comp = NewCompetition(sharedContestId);
        await FootballDataContext.Competitions.AddAsync(comp);
        await FootballDataContext.CompetitionCompetitors.AddRangeAsync(
            NewCompetitor(comp.Id, teamA.Id, "home"),
            NewCompetitor(comp.Id, teamB.Id, "away"));

        // A contest in the next season — reachable only via the excluded franchise season.
        var otherComp = NewCompetition(Guid.NewGuid());
        await FootballDataContext.Competitions.AddAsync(otherComp);
        await FootballDataContext.CompetitionCompetitors.AddAsync(
            NewCompetitor(otherComp.Id, teamNextSeason.Id, "home"));

        await FootballDataContext.SaveChangesAsync();

        var command = new RefreshContestsBySeasonYearCommand
        {
            Sport = Sport.FootballNcaa,
            SeasonYear = season,
            CorrelationId = Guid.NewGuid()
        };

        // act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // assert — exactly one enqueue: the shared contest deduped (home+away),
        // and the next-season contest excluded by the season filter.
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        background.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IUpdateContests, Task>>>()),
            Times.Exactly(1));
    }
}
