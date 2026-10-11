#nullable enable

using System.Linq.Expressions;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Commands.ReplayFootballContest;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Commands.ReplayFootballContest;

/// <summary>
/// The admin replay's pause between plays: passed from the query string into
/// the Hangfire job, defaulting to one second and limited to 1-60.
/// </summary>
public class ReplayContestIntervalTests
{
    private readonly Mock<IProvideBackgroundJobs> _jobs = new();
    private Expression<Func<IFootballContestReplayService, Task>>? _enqueued;

    private ContestController CreateController()
    {
        _jobs.Setup(x => x.Enqueue(It.IsAny<Expression<Func<IFootballContestReplayService, Task>>>()))
            .Callback<Expression<Func<IFootballContestReplayService, Task>>>(e => _enqueued = e)
            .Returns("job-1");

        var appMode = new Mock<IAppMode>();
        appMode.SetupGet(x => x.CurrentSport).Returns(Sport.FootballNcaa);

        // The replay endpoint never touches the data context.
        return new ContestController(NullLogger<ContestController>.Instance, _jobs.Object, null!, appMode.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    /// <summary>Runs the enqueued job against a mock service and returns the interval it was given.</summary>
    private async Task<int> EnqueuedInterval(Guid contestId)
    {
        _enqueued.Should().NotBeNull();
        var service = new Mock<IFootballContestReplayService>();
        await _enqueued!.Compile()(service.Object);

        var call = service.Invocations.Should().ContainSingle().Subject;
        call.Arguments[0].Should().Be(contestId);
        return (int)call.Arguments[2];
    }

    [Fact]
    public async Task Replay_PassesTheRequestedIntervalToTheJob()
    {
        var contestId = Guid.NewGuid();

        var result = CreateController().ReplayContestById(contestId, 5, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        (await EnqueuedInterval(contestId)).Should().Be(5);
    }

    [Fact]
    public async Task Replay_WithoutAnInterval_UsesTheDefault()
    {
        var contestId = Guid.NewGuid();

        var result = CreateController().ReplayContestById(contestId, null, CancellationToken.None);

        result.Should().BeOfType<AcceptedResult>();
        (await EnqueuedInterval(contestId)).Should().Be(FootballContestReplayService.DefaultIntervalSeconds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(61)]
    public void Replay_OutsideTheRange_IsRejectedAndNothingIsQueued(int intervalSeconds)
    {
        var result = CreateController().ReplayContestById(Guid.NewGuid(), intervalSeconds, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        _jobs.Invocations.Should().BeEmpty();
    }
}
