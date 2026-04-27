#nullable enable

using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// Tests for BaseballEventCompetitionStatusDocumentProcessor — the
/// MLB-specific processor that builds <c>BaseballCompetitionStatus</c>
/// (the sport-specific subclass) so the baseball-only fields and the
/// FeaturedAthletes child collection persist alongside the shared
/// status row.
///
/// The persistence path uses
/// <c>_dataContext.Set&lt;BaseballCompetitionStatus&gt;()</c>, which
/// requires the type to be in the model — only BaseballDataContext
/// has it registered. ProducerTestBase only wires up FootballDataContext
/// today, so the round-trip tests stay skipped behind the same
/// "Requires BaseballDataContext test infrastructure" guard the other
/// Baseball processor tests in this folder use. The DTO-deserialization
/// fact runs unconditionally and is the actual schema-drift canary
/// against ESPN's MLB status payload.
/// </summary>
[Collection("Sequential")]
public class BaseballEventCompetitionStatusDocumentProcessorTests
    : ProducerTestBase<BaseballEventCompetitionStatusDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task EspnBaseballEventCompetitionStatusDto_DeserializesMlbFields()
    {
        var json = await LoadJsonTestData("EspnBaseballMlb/EventCompetitionStatus.json");

        var dto = json.FromJson<EspnBaseballEventCompetitionStatusDto>();

        dto.Should().NotBeNull();
        dto!.Type.Name.Should().Be("STATUS_FINAL");
        dto.HalfInning.Should().Be(17);
        dto.PeriodPrefix.Should().Be("Bottom");
        dto.FeaturedAthletes.Should().HaveCount(2);
        dto.FeaturedAthletes![0].Name.Should().Be("winningPitcher");
        dto.FeaturedAthletes[0].PlayerId.Should().Be(4987924);
        dto.FeaturedAthletes[0].Athlete!.Ref.Should().NotBeNull();
        dto.FeaturedAthletes[1].Name.Should().Be("losingPitcher");
    }

    [Fact(Skip = "Requires BaseballDataContext test infrastructure")]
    public Task WhenNoExisting_PersistsStatus_WithMlbFieldsAndFeaturedAthletes()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires BaseballDataContext test infrastructure")]
    public Task WhenStatusTypeNameChanges_PublishesCompetitionStatusChanged()
    {
        return Task.CompletedTask;
    }

    [Fact(Skip = "Requires BaseballDataContext test infrastructure")]
    public Task WhenExistingStatusReplaced_OrdinalPreservesEspnSourceOrder()
    {
        return Task.CompletedTask;
    }
}
