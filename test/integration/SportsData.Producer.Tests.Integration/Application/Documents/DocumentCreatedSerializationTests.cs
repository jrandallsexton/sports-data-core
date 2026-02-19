using FluentAssertions;

using MassTransit;
using MassTransit.Testing;

using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;

using Xunit;

namespace SportsData.Producer.Tests.Integration.Application.Documents;

/// <summary>
/// Integration tests verifying DocumentCreated event serialization through MassTransit,
/// particularly ensuring RequestedDependencies tuple field names (Type, UrlHash) survive round-trip.
/// </summary>
public class DocumentCreatedSerializationTests
{
    /// <summary>
    /// No-op consumer for serialization testing - simply consumes messages without processing.
    /// Used to verify message serialization/deserialization without requiring handler dependencies.
    /// </summary>
    private class DocumentCreatedNoOpConsumer : IConsumer<DocumentCreated>
    {
        public Task Consume(ConsumeContext<DocumentCreated> context)
        {
            // No-op: Only needed for test harness to consume the message and validate serialization
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task DocumentCreated_SerializesAndDeserializes_WithRequestedDependenciesIntact()
    {
        // Arrange - Create test harness with no-op consumer for serialization testing
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentCreatedNoOpConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            // Create DocumentCreated with non-empty RequestedDependencies using record syntax
            var originalDependencies = new HashSet<RequestedDependency>
            {
                new(DocumentType.Franchise, "hash-franchise-123"),
                new(DocumentType.TeamSeason, "hash-team-season-456"),
                new(DocumentType.AthleteSeason, "hash-athlete-season-789")
            };

            var documentCreated = new DocumentCreated(
                Id: Guid.NewGuid().ToString(),
                ParentId: Guid.NewGuid().ToString(),
                Name: "TestDocument",
                Ref: new Uri("http://test.example.com/document/123"),
                SourceRef: new Uri("http://source.example.com/document/123"),
                DocumentJson: "{\"test\": \"data\"}",
                SourceUrlHash: "source-hash-123",
                Sport: Sport.FootballNcaa,
                SeasonYear: 2025,
                DocumentType: DocumentType.TeamSeasonLeaders,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid(),
                AttemptCount: 1,
                IncludeLinkedDocumentTypes: new[] { DocumentType.AthleteSeason },
                RequestedDependencies: originalDependencies
            );

            // Act - Publish through MassTransit
            await harness.Bus.Publish(documentCreated);

            // Wait for consumer to process (even though our test consumer is a no-op, this ensures serialization happened)
            var consumed = await harness.Consumed.Any<DocumentCreated>(x => x.Context.Message.Id == documentCreated.Id);

            // Assert - Message was consumed
            consumed.Should().BeTrue("DocumentCreated should be consumed through MassTransit");

            // Verify the consumed message has RequestedDependencies intact
            var consumedContext = harness.Consumed.Select<DocumentCreated>()
                .First(x => x.Context.Message.Id == documentCreated.Id);

            var deserializedMessage = consumedContext.Context.Message;

            // Critical assertions - verify tuple field names survived serialization
            deserializedMessage.RequestedDependencies.Should().NotBeNull("RequestedDependencies should be deserialized");
            deserializedMessage.RequestedDependencies.Should().HaveCount(3, "All three dependencies should be present");

            // Verify each tuple's Type and UrlHash fields are intact (not Item1/Item2)
            var deserializedList = deserializedMessage.RequestedDependencies!.ToList();

            // Check that we can access tuple fields by name (Type, UrlHash) not Item1/Item2
            var franchiseDep = deserializedList.Single(d => d.Type == DocumentType.Franchise);
            franchiseDep.Type.Should().Be(DocumentType.Franchise);
            franchiseDep.UrlHash.Should().Be("hash-franchise-123");

            var teamSeasonDep = deserializedList.Single(d => d.Type == DocumentType.TeamSeason);
            teamSeasonDep.Type.Should().Be(DocumentType.TeamSeason);
            teamSeasonDep.UrlHash.Should().Be("hash-team-season-456");

            var athleteSeasonDep = deserializedList.Single(d => d.Type == DocumentType.AthleteSeason);
            athleteSeasonDep.Type.Should().Be(DocumentType.AthleteSeason);
            athleteSeasonDep.UrlHash.Should().Be("hash-athlete-season-789");

            // Verify exact equality with original collection
            deserializedMessage.RequestedDependencies.Should()
                .BeEquivalentTo(originalDependencies, "Deserialized collection should match original");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task DocumentCreated_SerializesNullRequestedDependencies_WithoutError()
    {
        // Arrange - Test that null RequestedDependencies doesn't break serialization
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentCreatedNoOpConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var documentCreated = new DocumentCreated(
                Id: Guid.NewGuid().ToString(),
                ParentId: null,
                Name: "TestDocument",
                Ref: new Uri("http://test.example.com/document/456"),
                SourceRef: new Uri("http://source.example.com/document/456"),
                DocumentJson: "{\"test\": \"data\"}",
                SourceUrlHash: "source-hash-456",
                Sport: Sport.FootballNcaa,
                SeasonYear: 2025,
                DocumentType: DocumentType.Franchise,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid(),
                AttemptCount: 0,
                IncludeLinkedDocumentTypes: null,
                RequestedDependencies: null // Explicitly null
            );

            // Act
            await harness.Bus.Publish(documentCreated);

            // Assert
            var consumed = await harness.Consumed.Any<DocumentCreated>(x => x.Context.Message.Id == documentCreated.Id);
            consumed.Should().BeTrue("DocumentCreated with null RequestedDependencies should be consumed");

            var consumedContext = harness.Consumed.Select<DocumentCreated>()
                .First(x => x.Context.Message.Id == documentCreated.Id);

            consumedContext.Context.Message.RequestedDependencies.Should().BeNull("Null should remain null after deserialization");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task DocumentCreated_SerializesEmptyRequestedDependencies_WithoutError()
    {
        // Arrange - Test that empty HashSet works correctly
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(cfg =>
            {
                cfg.AddConsumer<DocumentCreatedNoOpConsumer>();
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var emptyDependencies = new HashSet<RequestedDependency>();

            var documentCreated = new DocumentCreated(
                Id: Guid.NewGuid().ToString(),
                ParentId: null,
                Name: "TestDocument",
                Ref: new Uri("http://test.example.com/document/789"),
                SourceRef: new Uri("http://source.example.com/document/789"),
                DocumentJson: "{\"test\": \"data\"}",
                SourceUrlHash: "source-hash-789",
                Sport: Sport.FootballNcaa,
                SeasonYear: 2025,
                DocumentType: DocumentType.Contest,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid(),
                AttemptCount: 0,
                IncludeLinkedDocumentTypes: null,
                RequestedDependencies: emptyDependencies
            );

            // Act
            await harness.Bus.Publish(documentCreated);

            // Assert
            var consumed = await harness.Consumed.Any<DocumentCreated>(x => x.Context.Message.Id == documentCreated.Id);
            consumed.Should().BeTrue("DocumentCreated with empty RequestedDependencies should be consumed");

            var consumedContext = harness.Consumed.Select<DocumentCreated>()
                .First(x => x.Context.Message.Id == documentCreated.Id);

            consumedContext.Context.Message.RequestedDependencies.Should().NotBeNull("Empty HashSet should not become null");
            consumedContext.Context.Message.RequestedDependencies.Should().BeEmpty("Empty HashSet should remain empty");
        }
        finally
        {
            await harness.Stop();
        }
    }
}
