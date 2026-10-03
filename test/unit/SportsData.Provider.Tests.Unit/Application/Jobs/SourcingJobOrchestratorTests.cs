using FluentAssertions;

using Hangfire;
using Hangfire.Common;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Infrastructure.Data.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Jobs;

/// <summary>
/// Recurring sourcing is UTC by convention. The exception is sourcing anchored
/// to a BROADCAST clock: the AP poll is released at 14:00 Eastern on Sunday and
/// the football season crosses the November DST boundary, so a fixed UTC cron
/// would drift an hour mid-season — while the API's matchup scheduler runs at
/// 14:30 Eastern expecting the poll to already be in the database.
/// </summary>
public class SourcingJobOrchestratorTests : ProviderTestBase<SourcingJobOrchestrator>
{
    public SourcingJobOrchestratorTests()
    {
        // The orchestrator only registers resources for the running sport.
        Mocker.GetMock<IAppMode>()
            .Setup(x => x.CurrentSport)
            .Returns(Sport.FootballNcaa);
    }

    [Fact]
    public async Task RecurringJob_WithNoTimeZone_RegistersInUtc()
    {
        await SeedRecurringResourceAsync(cron: "0 22 * * 0", timeZoneId: null!);

        var captured = CaptureRegistrations();

        await Mocker.CreateInstance<SourcingJobOrchestrator>().ExecuteAsync();

        captured.Should().ContainSingle();
        captured[0].TimeZone.Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public async Task RecurringJob_WithATimeZone_RegistersInThatZone()
    {
        await SeedRecurringResourceAsync(cron: "5 14 * * 0", timeZoneId: "America/New_York");

        var captured = CaptureRegistrations();

        await Mocker.CreateInstance<SourcingJobOrchestrator>().ExecuteAsync();

        captured.Should().ContainSingle();
        captured[0].TimeZone.Id.Should().Be(TimeZoneInfo.FindSystemTimeZoneById("America/New_York").Id);
    }

    /// <summary>
    /// This runs inside a loop registering EVERY resource index for the sport,
    /// so one malformed value must not cost the rest their sourcing. Degrade to
    /// UTC and log, rather than throw.
    /// </summary>
    [Fact]
    public async Task RecurringJob_WithAnUnresolvableTimeZone_FallsBackToUtc()
    {
        await SeedRecurringResourceAsync(cron: "5 14 * * 0", timeZoneId: "Mars/Olympus_Mons");

        var captured = CaptureRegistrations();

        await Mocker.CreateInstance<SourcingJobOrchestrator>().ExecuteAsync();

        captured.Should().ContainSingle("a bad zone must not prevent registration");
        captured[0].TimeZone.Should().Be(TimeZoneInfo.Utc);
    }

    private List<RecurringJobOptions> CaptureRegistrations()
    {
        var captured = new List<RecurringJobOptions>();

        Mocker.GetMock<IRecurringJobManager>()
            .Setup(x => x.AddOrUpdate(
                It.IsAny<string>(),
                It.IsAny<Job>(),
                It.IsAny<string>(),
                It.IsAny<RecurringJobOptions>()))
            .Callback<string, Job, string, RecurringJobOptions>((_, _, _, options) => captured.Add(options));

        return captured;
    }

    private async Task SeedRecurringResourceAsync(string cron, string timeZoneId)
    {
        DataContext.ResourceIndexJobs.Add(new ResourceIndex
        {
            Id = Guid.NewGuid(),
            Name = "espn.v2.sports.football.leagues.college-football.seasons.rankings",
            Ordinal = 1,
            IsRecurring = true,
            IsEnabled = true,
            CronExpression = cron,
            CronTimeZoneId = timeZoneId,
            SportId = Sport.FootballNcaa,
            Provider = SourceDataProvider.Espn,
            DocumentType = DocumentType.SeasonTypeWeekRankings,
            Uri = new Uri("https://example.invalid/rankings"),
            SourceUrlHash = "hash-" + Guid.NewGuid().ToString("N")
        });

        await DataContext.SaveChangesAsync();
    }
}
