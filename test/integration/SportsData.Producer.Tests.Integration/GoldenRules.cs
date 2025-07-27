using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Integration
{
    public class GoldenRules : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IServiceScope _scope;
        private readonly FootballDataContext _dataContext;

        public GoldenRules(IntegrationTestFixture fixture)
        {
            _scope = fixture.Services.CreateScope(); // ✅ Create scope
            _dataContext = _scope.ServiceProvider.GetRequiredService<FootballDataContext>(); // ✅ Resolve from scope
        }

        [Fact]
        public async Task Should_Connect_To_Postgres()
        {
            var canConnect = await _dataContext.Database.CanConnectAsync();
            canConnect.Should().BeTrue();
        }

        [Fact]
        public async Task Lsu_2024_Should_Have_13_Contests()
        {
            var fsId = await _dataContext.FranchiseSeasons
                .Where(fs => fs.Franchise.Slug == "lsu-tigers" && fs.SeasonYear == 2024)
                .Select(fs => fs.Id)
                .SingleAsync();

            var count = await _dataContext.Contests
                .CountAsync(c => c.HomeTeamFranchiseSeasonId == fsId || c.AwayTeamFranchiseSeasonId == fsId);

            count.Should().Be(13);
        }

        [Fact]
        public async Task UscAtLsu_2024_Should_Be_Correctly_Mapped()
        {
            var contest = await _dataContext.Contests
                .Include(x => x.Competitions)
                .ThenInclude(c => c.Competitors)
                .Include(x => x.Competitions)
                .ThenInclude(c => c.Links)
                .Include(c => c.Venue)
                .Include(c => c.HomeTeamFranchiseSeason)
                .ThenInclude(fs => fs.Franchise)
                .Include(c => c.AwayTeamFranchiseSeason)
                .ThenInclude(fs => fs.Franchise).Include(contest => contest.Competitions)
                .ThenInclude(competition => competition.Venue!)
                .Where(c => c.SeasonYear == 2024 && c.Name == "USC Trojans at LSU Tigers")
                .SingleOrDefaultAsync();

            contest.Should().NotBeNull("contest with name 'USC Trojans at LSU Tigers' and season 2024 should exist");
            contest!.Competitions.Should().ContainSingle();

            var competition = contest.Competitions.Single();
            competition.Attendance.Should().Be(63969);

            var homeTeam = contest.HomeTeamFranchiseSeason!.Franchise;
            var away = contest.AwayTeamFranchiseSeason!.Franchise;

            homeTeam.Should().NotBeNull();
            away.Should().NotBeNull();

            contest.HomeTeamFranchiseSeason!.SeasonYear.Should().Be(2024);
            homeTeam.Slug.Should().Be("lsu-tigers");

            competition.Competitors.First(x => x.HomeAway == "home").Winner.Should().BeFalse("LSU lost");
            competition.Competitors.First(x => x.HomeAway == "away").Winner.Should().BeTrue("USC won");

            competition.Links.Count.Should().Be(14);

            competition.Venue.Should().NotBeNull();
            competition.Venue!.Name.Should().Be("Allegiant Stadium");
        }


        public void Dispose()
        {
            _scope.Dispose(); // ✅ Clean up scope
        }
    }
}