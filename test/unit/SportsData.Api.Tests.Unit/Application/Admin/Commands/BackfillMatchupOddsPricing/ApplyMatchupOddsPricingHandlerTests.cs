using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Admin.Commands.BackfillMatchupOddsPricing;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.BackfillMatchupOddsPricing;

public class ApplyMatchupOddsPricingHandlerTests : ApiTestBase<ApplyMatchupOddsPricingHandler>
{
    private static readonly DateTime Kickoff = new(2026, 9, 26, 19, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly Guid _contestId = Guid.NewGuid();
    private readonly Mock<IProvideContests> _ncaaClient = new();

    public ApplyMatchupOddsPricingHandlerTests()
    {
        Mocker.GetMock<IContestClientFactory>().Setup(x => x.Resolve(Sport.FootballNcaa)).Returns(_ncaaClient.Object);
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
    }

    private async Task<Guid> SeedGroupAsync(Sport sport, League league)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = $"{sport} league", Sport = sport, League = league, CommissionerUserId = Guid.NewGuid()
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private async Task SeedMatchupAsync(Guid groupId, Guid contestId, double? overOdds = null)
    {
        await DataContext.PickemGroupMatchups.AddAsync(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(), GroupId = groupId, ContestId = contestId, SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026, SeasonWeek = 5, StartDateUtc = Kickoff, OverOdds = overOdds
        });
        await DataContext.SaveChangesAsync();
    }

    private void ProducerReturns(Result<OddsPricingDto> result) =>
        _ncaaClient.Setup(x => x.GetOddsPricingByContestId(_contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private Task RunAsync() =>
        Mocker.CreateInstance<ApplyMatchupOddsPricingHandler>()
            .Process(new ApplyMatchupOddsPricingCommand(Sport.FootballNcaa, _contestId, Guid.NewGuid()));

    [Fact]
    public async Task WritesEveryPrice_ToEveryMatchupOfTheContest_InThatSportOnly()
    {
        var leagueA = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var leagueB = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var otherContest = Guid.NewGuid();
        await SeedMatchupAsync(leagueA, _contestId);
        await SeedMatchupAsync(leagueB, _contestId);
        await SeedMatchupAsync(leagueA, otherContest);
        ProducerReturns(new Success<OddsPricingDto>(new OddsPricingDto
        {
            ContestId = _contestId,
            AwayMoneyLine = 240, HomeMoneyLine = -300,
            AwaySpreadPrice = -112m, HomeSpreadPrice = -108m,
            OverOdds = -115m, UnderOdds = -105m
        }));

        await RunAsync();

        var matchups = await DataContext.PickemGroupMatchups.ToListAsync();
        foreach (var m in matchups.Where(m => m.ContestId == _contestId))
        {
            Assert.Equal(240, m.AwayMoneyLine);
            Assert.Equal(-300, m.HomeMoneyLine);
            Assert.Equal(-112d, m.AwaySpreadPrice);
            Assert.Equal(-108d, m.HomeSpreadPrice);
            Assert.Equal(-115d, m.OverOdds);
            Assert.Equal(-105d, m.UnderOdds);
        }
        Assert.Equal(2, matchups.Count(m => m.ContestId == _contestId && m.AwayMoneyLine == 240));
        Assert.Null(matchups.Single(m => m.ContestId == otherContest).AwayMoneyLine);
    }

    [Fact]
    public async Task NeverErases_AValueTheProducerDoesNotSupply()
    {
        // The contest's odds are gone upstream (all prices null): the matchup
        // keeps the OverOdds it already had.
        await SeedMatchupAsync(await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF), _contestId, overOdds: -110);
        ProducerReturns(new Success<OddsPricingDto>(new OddsPricingDto { ContestId = _contestId }));

        await RunAsync();

        var m = Assert.Single(await DataContext.PickemGroupMatchups.ToListAsync());
        Assert.Equal(-110d, m.OverOdds);
        Assert.Null(m.AwayMoneyLine);
    }

    [Fact]
    public async Task ContestNotFound_LeavesMatchupsUnchanged_AndDoesNotThrow()
    {
        // Retrying cannot help a contest the Producer does not have.
        await SeedMatchupAsync(await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF), _contestId, overOdds: -110);
        ProducerReturns(new Failure<OddsPricingDto>(default!, ResultStatus.NotFound, [new ValidationFailure("contestId", "not found")]));

        await RunAsync();

        var m = Assert.Single(await DataContext.PickemGroupMatchups.ToListAsync());
        Assert.Equal(-110d, m.OverOdds);
        Assert.Null(m.AwayMoneyLine);
    }

    [Fact]
    public async Task ProducerError_Throws_SoHangfireRetriesThisContest()
    {
        await SeedMatchupAsync(await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF), _contestId);
        ProducerReturns(new Failure<OddsPricingDto>(default!, ResultStatus.Error, [new ValidationFailure("Odds pricing", "Producer returned 500")]));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);

        Assert.Contains(_contestId.ToString(), ex.Message);
        Assert.Null(Assert.Single(await DataContext.PickemGroupMatchups.ToListAsync()).AwayMoneyLine);
    }

    [Fact]
    public async Task StampsModifiedUtc_OnlyWhenAPriceChanged()
    {
        await SeedMatchupAsync(await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF), _contestId);
        ProducerReturns(new Success<OddsPricingDto>(new OddsPricingDto
        {
            ContestId = _contestId, AwayMoneyLine = 240, HomeMoneyLine = -300
        }));

        await RunAsync();
        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(Now, m.ModifiedUtc);
        Assert.Equal(Guid.Empty, m.ModifiedBy);

        // A re-run with the same prices changes nothing: the stamp must not move.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now.AddHours(1));
        await RunAsync();
        m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(Now, m.ModifiedUtc);
    }
}
