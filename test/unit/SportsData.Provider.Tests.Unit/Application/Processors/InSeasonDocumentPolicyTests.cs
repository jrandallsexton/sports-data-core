using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Provider.Application.Processors;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Processors;

public class InSeasonDocumentPolicyTests
{
    [Theory]
    // Immutable once created → served from Mongo even in-season (the fix).
    [InlineData(DocumentType.EventCompetitionPlay, true)]
    // One probability item per play; append-only index, completed entries never
    // change. Excluding it sent every item to ESPN each 15s live cycle
    // (611,931 processed 2026-09-06 for ~500 real items).
    [InlineData(DocumentType.EventCompetitionProbability, true)]
    // Mutable aggregates → must keep bypassing cache in-season to stay fresh.
    [InlineData(DocumentType.EventCompetitionStatus, false)]
    [InlineData(DocumentType.EventCompetitionSituation, false)]
    [InlineData(DocumentType.EventCompetitionCompetitorScore, false)]
    [InlineData(DocumentType.EventCompetitionCompetitorRecord, false)]
    [InlineData(DocumentType.EventCompetitionOdds, false)]
    [InlineData(DocumentType.EventCompetition, false)]
    [InlineData(DocumentType.Athlete, false)]
    public void IsImmutableInSeason_ClassifiesCorrectly(DocumentType type, bool expected)
    {
        InSeasonDocumentPolicy.IsImmutableInSeason(type).Should().Be(expected);
    }
}
