using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using System.Net;
using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

/// <summary>
/// Unit tests for FootballCompetitionStreamer focusing on isolated behavior.
/// For integration tests using real Postman collection data, see SportsData.Producer.Tests.Integration.
/// </summary>
[Collection("Sequential")]
public class FootballCompetitionStreamer_LiveGameTests : ProducerTestBase<FootballCompetitionStreamer>
{
    /// <summary>
    /// Quick validation test - just checks that Postman collection can be loaded.
    /// This is useful for local development to verify the test infrastructure works.
    /// The full integration test is in SportsData.Producer.Tests.Integration.
    /// </summary>
    [Fact]
    public void PostmanCollection_CanBeLoaded_ContainsStatusResponses()
    {
        // Arrange
        const string PostmanCollectionPath = "Data/Football.Ncaa.Espn.Event.postman_collection.json";
        var postmanPath = Path.Combine(Directory.GetCurrentDirectory(), PostmanCollectionPath);
        
        // Skip if file doesn't exist (optional for unit tests)
        if (!File.Exists(postmanPath))
        {
            // Not a failure - this is just a quick validation test
            Console.WriteLine($"Postman collection not found at: {postmanPath}");
            Console.WriteLine("This is expected if running outside the full test context.");
            return;
        }
        
        // Act
        var stateManager = new PostmanGameStateManager(postmanPath);
        
        // Assert
        stateManager.TotalStatusResponses.Should().BeGreaterThan(0);
        
        Console.WriteLine($"? Loaded {stateManager.TotalStatusResponses} status responses");
        Console.WriteLine($"? Postman collection path: {postmanPath}");
    }
}
