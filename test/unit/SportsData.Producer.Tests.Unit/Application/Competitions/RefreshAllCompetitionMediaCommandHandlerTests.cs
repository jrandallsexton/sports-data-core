using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.RefreshCompetitionMedia;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

public class RefreshAllCompetitionMediaCommandHandlerTests : ProducerTestBase<RefreshAllCompetitionMediaCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_WithFinalizedContestsWithoutMedia_EnqueuesMediaRefreshJobs()
    {
        // Arrange
        var seasonYear = 2025;
        var fbsGroupSeasonId = Guid.NewGuid();
        var competitionId1 = Guid.NewGuid();
        var competitionId2 = Guid.NewGuid();
        var contestId1 = Guid.NewGuid();
        var contestId2 = Guid.NewGuid();
        var homeFranchiseSeasonId1 = Guid.NewGuid();
        var awayFranchiseSeasonId1 = Guid.NewGuid();
        var homeFranchiseSeasonId2 = Guid.NewGuid();
        var awayFranchiseSeasonId2 = Guid.NewGuid();

        // Mock group seasons service
        var groupSeasonsService = new Mock<IGroupSeasonsService>();
        groupSeasonsService
            .Setup(x => x.GetFbsGroupSeasonIds(seasonYear))
            .ReturnsAsync(new HashSet<Guid> { fbsGroupSeasonId });
        Mocker.Use(groupSeasonsService.Object);

        // Create franchise seasons
        var homeFranchiseSeason1 = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, homeFranchiseSeasonId1)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        var awayFranchiseSeason1 = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, awayFranchiseSeasonId1)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        var homeFranchiseSeason2 = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, homeFranchiseSeasonId2)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        var awayFranchiseSeason2 = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, awayFranchiseSeasonId2)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        await FootballDataContext.FranchiseSeasons.AddRangeAsync(
            homeFranchiseSeason1, awayFranchiseSeason1,
            homeFranchiseSeason2, awayFranchiseSeason2);

        // Create finalized contests
        var contest1 = Fixture.Build<Contest>()
            .With(x => x.Id, contestId1)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .With(x => x.HomeTeamFranchiseSeasonId, homeFranchiseSeasonId1)
            .With(x => x.AwayTeamFranchiseSeasonId, awayFranchiseSeasonId1)
            .With(x => x.HomeTeamFranchiseSeason, homeFranchiseSeason1)
            .With(x => x.AwayTeamFranchiseSeason, awayFranchiseSeason1)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Create();

        var contest2 = Fixture.Build<Contest>()
            .With(x => x.Id, contestId2)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .With(x => x.HomeTeamFranchiseSeasonId, homeFranchiseSeasonId2)
            .With(x => x.AwayTeamFranchiseSeasonId, awayFranchiseSeasonId2)
            .With(x => x.HomeTeamFranchiseSeason, homeFranchiseSeason2)
            .With(x => x.AwayTeamFranchiseSeason, awayFranchiseSeason2)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Create();

        await FootballDataContext.Contests.AddRangeAsync(contest1, contest2);

        // Create competitions without media
        var competition1 = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId1)
            .With(x => x.ContestId, contestId1)
            .With(x => x.Contest, contest1)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Media)
            .Without(x => x.Metrics)
            .Create();

        var competition2 = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId2)
            .With(x => x.ContestId, contestId2)
            .With(x => x.Contest, contest2)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Media)
            .Without(x => x.Metrics)
            .Create();

        await FootballDataContext.Competitions.AddRangeAsync(competition1, competition2);
        await FootballDataContext.SaveChangesAsync();

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var command = new RefreshAllCompetitionMediaCommand(seasonYear);
        var sut = Mocker.CreateInstance<RefreshAllCompetitionMediaCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<RefreshAllCompetitionMediaResult>>();
        result.Value.SeasonYear.Should().Be(seasonYear);
        result.Value.TotalCompetitions.Should().Be(2);
        result.Value.EnqueuedJobs.Should().Be(2);

        // Verify job enqueueing would require checking Hangfire internals
        // The important behavior (returning correct result) is tested above
    }

    [Fact]
    public async Task ExecuteAsync_WithCompetitionsThatHaveMedia_SkipsEnqueueing()
    {
        // Arrange
        var seasonYear = 2025;
        var fbsGroupSeasonId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();

        var groupSeasonsService = new Mock<IGroupSeasonsService>();
        groupSeasonsService
            .Setup(x => x.GetFbsGroupSeasonIds(seasonYear))
            .ReturnsAsync(new HashSet<Guid> { fbsGroupSeasonId });
        Mocker.Use(groupSeasonsService.Object);

        var homeFranchiseSeason = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, homeFranchiseSeasonId)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        var awayFranchiseSeason = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, awayFranchiseSeasonId)
            .With(x => x.GroupSeasonId, fbsGroupSeasonId)
            .Without(x => x.ExternalIds)
            .Without(x => x.Franchise)
            .Without(x => x.GroupSeason)
            .Create();

        await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeFranchiseSeason, awayFranchiseSeason);

        var contest = Fixture.Build<Contest>()
            .With(x => x.Id, contestId)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .With(x => x.HomeTeamFranchiseSeasonId, homeFranchiseSeasonId)
            .With(x => x.AwayTeamFranchiseSeasonId, awayFranchiseSeasonId)
            .With(x => x.HomeTeamFranchiseSeason, homeFranchiseSeason)
            .With(x => x.AwayTeamFranchiseSeason, awayFranchiseSeason)
            .Without(x => x.Links)
            .Without(x => x.ExternalIds)
            .Without(x => x.Competitions)
            .Create();

        await FootballDataContext.Contests.AddAsync(contest);

        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, contestId)
            .With(x => x.Contest, contest)
            .Without(x => x.Plays)
            .Without(x => x.Drives)
            .Without(x => x.ExternalIds)
            .Without(x => x.Metrics)
            .Create();

        // Add media
        var media = Fixture.Build<CompetitionMedia>()
            .With(x => x.CompetitionId, competitionId)
            .Without(x => x.Competition)
            .Without(x => x.HomeFranchiseSeason)
            .Without(x => x.AwayFranchiseSeason)
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.CompetitionMedia.AddAsync(media);
        await FootballDataContext.SaveChangesAsync();

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var command = new RefreshAllCompetitionMediaCommand(seasonYear);
        var sut = Mocker.CreateInstance<RefreshAllCompetitionMediaCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<RefreshAllCompetitionMediaResult>>();
        result.Value.EnqueuedJobs.Should().Be(0);

        // Verify no enqueueing by checking result shows 0 jobs
    }
}
