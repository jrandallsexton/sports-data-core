#nullable enable

using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Baseball;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

/// <summary>
/// Tests for BaseballCompetitionStreamer's polling-target declarations.
/// Mirrors the FootballCompetitionStreamerTests "Polling Targets" region;
/// kept in a sibling file because the SUT requires BaseballDataContext rather
/// than the football one wired by ProducerTestBase.
/// </summary>
public class BaseballCompetitionStreamerTests
    : ProducerTestBase<BaseballCompetitionStreamer>
{
    private readonly BaseballDataContext _baseballDataContext;

    public BaseballCompetitionStreamerTests()
    {
        _baseballDataContext = new BaseballDataContext(
            new DbContextOptionsBuilder<BaseballDataContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
                .Options);
        Mocker.Use(_baseballDataContext);
    }

    // Test-only subclass exposing the protected GetPollingTargets method.
    private sealed class TestableBaseballCompetitionStreamer : BaseballCompetitionStreamer
    {
        public TestableBaseballCompetitionStreamer(
            ILogger<BaseballCompetitionStreamer> logger,
            BaseballDataContext dataContext,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IDateTimeProvider dateTimeProvider)
            : base(logger, dataContext, httpClientFactory, scopeFactory, dateTimeProvider)
        {
        }

        public IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds, bool RequiresParentId)>
            InvokeGetPollingTargets(EspnBaseballEventCompetitionDto dto)
            => GetPollingTargets(dto);
    }

    private static EspnBaseballEventCompetitionDto BuildFullyLinkedDto() => new()
    {
        Probabilities = new EspnLinkDto { Ref = new Uri("http://test/probabilities") },
        Details       = new EspnLinkDto { Ref = new Uri("http://test/plays") },
        Situation     = new EspnLinkDto { Ref = new Uri("http://test/situation") },
        Leaders       = new EspnLinkDto { Ref = new Uri("http://test/leaders") },
    };

    [Fact]
    public void GetPollingTargets_ReturnsFourTargets_ForBaseball()
    {
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = BuildFullyLinkedDto();

        var targets = sut.InvokeGetPollingTargets(dto).ToList();

        targets.Should().HaveCount(4);
        targets.Select(t => t.DocumentType).Should().BeEquivalentTo(new[]
        {
            DocumentType.EventCompetitionProbability,
            DocumentType.EventCompetitionPlay,
            DocumentType.EventCompetitionSituation,
            DocumentType.EventCompetitionLeaders,
        });
    }

    [Fact]
    public void GetPollingTargets_DoesNotIncludeDrive()
    {
        // Drives are a football-only concept; baseball must not poll them.
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = BuildFullyLinkedDto();

        var targets = sut.InvokeGetPollingTargets(dto).ToList();

        targets.Should().NotContain(t => t.DocumentType == DocumentType.EventCompetitionDrive);
    }

    [Fact]
    public void GetPollingTargets_FlagsParentIdPerProcessorAudit()
    {
        // Same audit (2026-05-15) as the football counterpart: only Probability
        // is `false`. The other three call TryGetOrDeriveParentId downstream.
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = BuildFullyLinkedDto();

        var byType = sut.InvokeGetPollingTargets(dto).ToDictionary(t => t.DocumentType);

        byType[DocumentType.EventCompetitionProbability].RequiresParentId.Should().BeFalse();
        byType[DocumentType.EventCompetitionPlay].RequiresParentId.Should().BeTrue();
        byType[DocumentType.EventCompetitionSituation].RequiresParentId.Should().BeTrue();
        byType[DocumentType.EventCompetitionLeaders].RequiresParentId.Should().BeTrue();
    }

    [Fact]
    public void GetPollingTargets_ReturnsExpectedIntervalsForBaseball()
    {
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = BuildFullyLinkedDto();

        var byType = sut.InvokeGetPollingTargets(dto).ToDictionary(t => t.DocumentType);

        byType[DocumentType.EventCompetitionProbability].IntervalSeconds.Should().Be(60);
        byType[DocumentType.EventCompetitionPlay].IntervalSeconds.Should().Be(30);
        byType[DocumentType.EventCompetitionSituation].IntervalSeconds.Should().Be(30);
        byType[DocumentType.EventCompetitionLeaders].IntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void GetPollingTargets_PassesThroughLinkRefUris()
    {
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = BuildFullyLinkedDto();

        var byType = sut.InvokeGetPollingTargets(dto).ToDictionary(t => t.DocumentType);

        byType[DocumentType.EventCompetitionProbability].RefUri.Should().Be(new Uri("http://test/probabilities"));
        byType[DocumentType.EventCompetitionPlay].RefUri.Should().Be(new Uri("http://test/plays"));
        byType[DocumentType.EventCompetitionSituation].RefUri.Should().Be(new Uri("http://test/situation"));
        byType[DocumentType.EventCompetitionLeaders].RefUri.Should().Be(new Uri("http://test/leaders"));
    }

    [Fact]
    public void GetPollingTargets_ReturnsNullRefUri_WhenLinkAbsent()
    {
        var sut = Mocker.CreateInstance<TestableBaseballCompetitionStreamer>();
        var dto = new EspnBaseballEventCompetitionDto(); // all links null

        var targets = sut.InvokeGetPollingTargets(dto).ToList();

        targets.Should().HaveCount(4, "shape is fixed; null links surface as null RefUri");
        targets.Should().AllSatisfy(t => t.RefUri.Should().BeNull());
    }
}
