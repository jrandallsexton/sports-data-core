using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Competitions;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using System.Net;
using Xunit;

namespace SportsData.Producer.Tests.Integration.Application.Competitions;

/// <summary>
/// Integration tests using real captured game data from Postman collections.
/// Tests the complete FootballCompetitionStreamer workflow with actual ESPN API response data.
/// </summary>
[Collection("Sequential")]
public class FootballCompetitionStreamer_LiveGameTests : IClassFixture<IntegrationTestFixture>, IDisposable
{
    private const string PostmanCollectionPath = "Data/Football.Ncaa.Espn.Event.postman_collection.json";
    
    private readonly IServiceScope _scope;
    private readonly FootballDataContext _dataContext;
    private readonly IServiceProvider _serviceProvider;
    
    public FootballCompetitionStreamer_LiveGameTests(IntegrationTestFixture fixture)
    {
        _scope = fixture.Services.CreateScope();
        _serviceProvider = _scope.ServiceProvider;
        _dataContext = _scope.ServiceProvider.GetRequiredService<FootballDataContext>();
    }
    
    public void Dispose()
    {
        _scope.Dispose();
    }
    
    /// <summary>
    /// This test uses ACTUAL Postman-captured data to simulate a complete game!
    /// 
    /// ZERO SETUP REQUIRED - just run the test!
    /// The PostmanGameStateManager reads all 18 status responses directly from the
    /// Postman collection file.
    /// 
    /// Game Data: Iowa State @ Kansas State (Event 401756846)
    /// Status Snapshots: 18 (Q1 start ? Q2 ? Halftime ? Q3 ? Q4 ? Final)
    /// </summary>
    [Fact]
    public async Task StreamCompleteGame_UsingPostmanCollection_CompletesSuccessfully()
    {
        // Arrange
        var postmanPath = GetPostmanCollectionPath();
        
        // Verify Postman collection exists
        if (!File.Exists(postmanPath))
        {
            throw new FileNotFoundException(
                $"Postman collection not found: {postmanPath}\n" +
                $"Expected location: test/integration/SportsData.Producer.Tests.Integration/{PostmanCollectionPath}");
        }
        
        // Create game state manager directly from Postman collection
        var stateManager = new PostmanGameStateManager(postmanPath);
        
        // Verify status responses were loaded
        stateManager.TotalStatusResponses.Should().BeGreaterThan(0, 
            "Postman collection should contain status responses");
        
        Console.WriteLine($"? Loaded {stateManager.TotalStatusResponses} status responses from Postman collection");
        
        // Setup HTTP handler with Postman data
        var handler = new PostmanStateManagedHttpHandler(stateManager);
        var httpClient = new HttpClient(handler);
        
        var httpFactory = new TestHttpClientFactory(httpClient);
        
        // Setup event bus to track published events
        var eventBus = new TestEventBus();
        
        // Create test game data in the database
        var (contest, competition, stream) = await CreateTestGameAsync();
        
        var command = new StreamFootballCompetitionCommand
        {
            CompetitionId = competition.Id,
            ContestId = contest.Id,
            Sport = Sport.FootballNcaa,
            SeasonYear = 2024,
            DataProvider = SourceDataProvider.Espn,
            CorrelationId = Guid.NewGuid()
        };
        
        // Create the streamer with test dependencies
        var logger = _serviceProvider.GetRequiredService<ILogger<FootballCompetitionStreamer>>();
        var sut = new FootballCompetitionStreamer(
            logger,
            _dataContext,
            httpFactory,
            eventBus);
        
        // Act - Run the complete game stream
        // With 18 status responses × ~30 second polling = ~9 minutes max
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        
        await sut.ExecuteAsync(command, cts.Token);
        
        // Assert - Verify game completed successfully
        var finalStream = await _dataContext.CompetitionStreams
            .FirstAsync(s => s.CompetitionId == competition.Id);
        
        finalStream.Status.Should().Be(CompetitionStreamStatus.Completed,
            "game should complete when status reaches FINAL");
        finalStream.StreamStartedUtc.Should().NotBeNull(
            "stream start time should be recorded");
        finalStream.StreamEndedUtc.Should().NotBeNull(
            "stream end time should be recorded");
        
        // Verify all status responses were processed
        stateManager.StatusCallCount.Should().BeGreaterThan(0,
            "status endpoint should have been polled");
        
        Console.WriteLine($"? Status calls made: {stateManager.StatusCallCount}/{stateManager.TotalStatusResponses}");
        Console.WriteLine($"? Final game state: {stateManager.CurrentGameState}");
        
        // Verify workers published document requests
        eventBus.PublishedEvents.Should().Contain(e => e.DocumentType == DocumentType.EventCompetitionSituation,
            "situation worker should have published requests");
        eventBus.PublishedEvents.Should().Contain(e => e.DocumentType == DocumentType.EventCompetitionPlay,
            "play worker should have published requests");
        
        Console.WriteLine($"? Published {eventBus.PublishedEvents.Count} document requests");
        
        // Verify call distribution
        var callCounts = handler.CallCounts;
        callCounts["status"].Should().BeGreaterThan(1,
            "status should be polled multiple times");
        callCounts["competition"].Should().BeGreaterThan(0,
            "competition document should be fetched");
        
        Console.WriteLine($"? HTTP calls - Status: {callCounts["status"]}, Competition: {callCounts["competition"]}");
    }
    
    /// <summary>
    /// Quick validation test - just checks that Postman collection can be loaded.
    /// Useful for verifying test infrastructure without running the full integration test.
    /// </summary>
    [Fact]
    public void PostmanCollection_CanBeLoaded_ContainsStatusResponses()
    {
        // Arrange
        var postmanPath = GetPostmanCollectionPath();
        
        // Act
        var stateManager = new PostmanGameStateManager(postmanPath);
        
        // Assert
        stateManager.TotalStatusResponses.Should().BeGreaterThan(0);
        
        Console.WriteLine($"? Loaded {stateManager.TotalStatusResponses} status responses");
        Console.WriteLine($"? Postman collection path: {postmanPath}");
    }
    
    private string GetPostmanCollectionPath()
    {
        // Data folder is in the integration test project itself
        var currentDir = Directory.GetCurrentDirectory();
        var postmanPath = Path.Combine(currentDir, PostmanCollectionPath);
        
        return postmanPath;
    }
    
    private async Task<(Contest contest, Competition competition, CompetitionStream stream)> CreateTestGameAsync(
        Guid? competitionId = null,
        Guid? contestId = null)
    {
        var compId = competitionId ?? Guid.NewGuid();
        var contId = contestId ?? Guid.NewGuid();

        // Create test franchise seasons (required for Contest foreign keys)
        var homeFranchiseSeasonId = Guid.NewGuid();
        var awayFranchiseSeasonId = Guid.NewGuid();
        
        var homeFranchise = new Franchise
        {
            Id = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            Name = "Kansas State Wildcats",
            Nickname = "Wildcats",
            Abbreviation = "KSU",
            Location = "Manhattan",
            DisplayName = "Kansas State Wildcats",
            DisplayNameShort = "K-State",
            ColorCodeHex = "#512888",
            IsActive = true,
            Slug = "kansas-state-wildcats",
            VenueId = Guid.NewGuid(), // Dummy venue
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        
        var awayFranchise = new Franchise
        {
            Id = Guid.NewGuid(),
            Sport = Sport.FootballNcaa,
            Name = "Iowa State Cyclones",
            Nickname = "Cyclones",
            Abbreviation = "ISU",
            Location = "Ames",
            DisplayName = "Iowa State Cyclones",
            DisplayNameShort = "Iowa St",
            ColorCodeHex = "#C8102E",
            IsActive = true,
            Slug = "iowa-state-cyclones",
            VenueId = Guid.NewGuid(), // Dummy venue
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var homeFranchiseSeason = new FranchiseSeason
        {
            Id = homeFranchiseSeasonId,
            FranchiseId = homeFranchise.Id,
            Franchise = homeFranchise,
            SeasonYear = 2024,
            Slug = "kansas-state-wildcats",
            Location = "Manhattan",
            Name = "Wildcats",
            Abbreviation = "KSU",
            DisplayName = "Kansas State Wildcats",
            DisplayNameShort = "K-State",
            ColorCodeHex = "#512888",
            IsActive = true,
            IsAllStar = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        
        var awayFranchiseSeason = new FranchiseSeason
        {
            Id = awayFranchiseSeasonId,
            FranchiseId = awayFranchise.Id,
            Franchise = awayFranchise,
            SeasonYear = 2024,
            Slug = "iowa-state-cyclones",
            Location = "Ames",
            Name = "Cyclones",
            Abbreviation = "ISU",
            DisplayName = "Iowa State Cyclones",
            DisplayNameShort = "Iowa St",
            ColorCodeHex = "#C8102E",
            IsActive = true,
            IsAllStar = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Create Season, SeasonPhase, and SeasonWeek (required for Contest)
        var season = new Season
        {
            Id = Guid.NewGuid(),
            Year = 2024,
            Name = "2024",
            StartDate = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        
        var seasonPhase = new SeasonPhase
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Season = season,
            TypeCode = 2, // Regular season
            Name = "Regular Season",
            Abbreviation = "REG",
            Slug = "regular-season",
            Year = 2024,
            StartDate = new DateTime(2024, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 11, 30, 0, 0, 0, DateTimeKind.Utc),
            HasGroups = true,
            HasStandings = true,
            HasLegs = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        
        var seasonWeek = new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = season.Id,
            Season = season,
            SeasonPhaseId = seasonPhase.Id,
            SeasonPhase = seasonPhase,
            Number = 10,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var contest = new Contest
        {
            Id = contId,
            Name = "Iowa State @ Kansas State",
            ShortName = "ISU@KSU",
            SeasonYear = 2024,
            Sport = Sport.FootballNcaa,
            StartDateUtc = DateTime.UtcNow.AddHours(1),
            HomeTeamFranchiseSeasonId = homeFranchiseSeasonId,
            AwayTeamFranchiseSeasonId = awayFranchiseSeasonId,
            SeasonWeekId = seasonWeek.Id,
            SeasonPhaseId = seasonPhase.Id,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var competition = new Competition
        {
            Id = compId,
            ContestId = contest.Id,
            Contest = contest,
            Date = DateTime.UtcNow.AddHours(1),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = compId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846",
                    SourceUrlHash = "test-hash-123",
                    Value = "401756846",
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        var stream = new CompetitionStream
        {
            Id = Guid.NewGuid(),
            CompetitionId = compId,
            Competition = competition,
            SeasonWeekId = seasonWeek.Id,
            ScheduledTimeUtc = DateTime.UtcNow,
            BackgroundJobId = "test-job-123",
            Status = CompetitionStreamStatus.Scheduled,
            RetryCount = 0,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // Add all entities in correct order
        await _dataContext.Franchises.AddAsync(homeFranchise);
        await _dataContext.Franchises.AddAsync(awayFranchise);
        await _dataContext.FranchiseSeasons.AddRangeAsync(homeFranchiseSeason, awayFranchiseSeason);
        await _dataContext.Seasons.AddAsync(season);
        await _dataContext.SeasonPhases.AddAsync(seasonPhase);
        await _dataContext.SeasonWeeks.AddAsync(seasonWeek);
        await _dataContext.Contests.AddAsync(contest);
        await _dataContext.Competitions.AddAsync(competition);
        await _dataContext.CompetitionStreams.AddAsync(stream);
        await _dataContext.SaveChangesAsync();

        _dataContext.ChangeTracker.Clear();

        return (contest, competition, stream);
    }
}

// Test helper classes

/// <summary>
/// Test implementation of IHttpClientFactory that returns a pre-configured HttpClient
/// </summary>
public class TestHttpClientFactory : IHttpClientFactory
{
    private readonly HttpClient _httpClient;
    
    public TestHttpClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public HttpClient CreateClient(string name) => _httpClient;
}

/// <summary>
/// Test implementation of IEventBus that tracks published events
/// </summary>
public class TestEventBus : IEventBus
{
    public List<DocumentRequested> PublishedEvents { get; } = new();
    
    public Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        if (message is DocumentRequested docReq)
        {
            PublishedEvents.Add(docReq);
        }
        return Task.CompletedTask;
    }
    
    public Task PublishBatch<T>(IEnumerable<T> messages, CancellationToken ct = default) where T : class
    {
        foreach (var message in messages)
        {
            if (message is DocumentRequested docReq)
            {
                PublishedEvents.Add(docReq);
            }
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Manages game state progression by reading status responses directly from a Postman collection.
/// This eliminates the need to manually extract individual JSON files.
/// </summary>
public class PostmanGameStateManager
{
    private int _statusCallCount = 0;
    private readonly List<string> _statusResponses = new();
    private readonly string _postmanCollectionPath;
    
    public int StatusCallCount => _statusCallCount;
    public string CurrentGameState { get; private set; } = "Unknown";
    
    /// <summary>
    /// Creates a new PostmanGameStateManager from a Postman collection file.
    /// </summary>
    /// <param name="postmanCollectionPath">Path to the .postman_collection.json file</param>
    /// <param name="statusRequestName">Name of the Status request in the collection (default: "Status")</param>
    public PostmanGameStateManager(string postmanCollectionPath, string statusRequestName = "Status")
    {
        _postmanCollectionPath = postmanCollectionPath;
        LoadStatusResponsesFromPostman(statusRequestName);
    }
    
    private void LoadStatusResponsesFromPostman(string statusRequestName)
    {
        if (!File.Exists(_postmanCollectionPath))
        {
            throw new FileNotFoundException($"Postman collection not found: {_postmanCollectionPath}");
        }
        
        var json = File.ReadAllText(_postmanCollectionPath);
        var doc = System.Text.Json.JsonDocument.Parse(json);
        
        // Find the Status request in the collection
        var items = doc.RootElement.GetProperty("item");
        
        System.Text.Json.JsonElement? statusItem = null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var name) && 
                name.GetString() == statusRequestName)
            {
                statusItem = item;
                break;
            }
        }
        
        if (statusItem == null)
        {
            throw new InvalidOperationException($"Could not find request named '{statusRequestName}' in Postman collection");
        }
        
        // Extract all response bodies
        if (statusItem.Value.TryGetProperty("response", out var responses))
        {
            foreach (var response in responses.EnumerateArray())
            {
                if (response.TryGetProperty("body", out var body))
                {
                    var bodyText = body.GetString();
                    if (!string.IsNullOrWhiteSpace(bodyText))
                    {
                        _statusResponses.Add(bodyText);
                    }
                }
            }
        }
        
        if (_statusResponses.Count == 0)
        {
            throw new InvalidOperationException("No status responses found in Postman collection");
        }
    }
    
    /// <summary>
    /// Gets the next status response, advancing the game state.
    /// </summary>
    public string GetNextStatusResponse()
    {
        if (_statusResponses.Count == 0)
        {
            throw new InvalidOperationException("No status responses loaded");
        }
        
        // Return final status repeatedly after game ends
        if (_statusCallCount >= _statusResponses.Count)
        {
            return _statusResponses[^1];
        }
        
        var response = _statusResponses[_statusCallCount];
        _statusCallCount++;
        
        // Update game state based on response
        UpdateGameState(response);
        
        return response;
    }
    
    private void UpdateGameState(string statusJson)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(statusJson);
            var typeName = doc.RootElement
                .GetProperty("type")
                .GetProperty("name")
                .GetString();
            
            CurrentGameState = typeName ?? "Unknown";
        }
        catch
        {
            // Ignore parse errors
        }
    }
    
    /// <summary>
    /// Resets the game state to the beginning.
    /// </summary>
    public void Reset()
    {
        _statusCallCount = 0;
        CurrentGameState = "Unknown";
    }
    
    /// <summary>
    /// Gets the total number of status responses loaded.
    /// </summary>
    public int TotalStatusResponses => _statusResponses.Count;
}

/// <summary>
/// Custom HttpMessageHandler that uses PostmanGameStateManager to provide sequential responses.
/// Simulates ESPN API behavior during a live game using data from a Postman collection.
/// </summary>
public class PostmanStateManagedHttpHandler : HttpMessageHandler
{
    private readonly PostmanGameStateManager _stateManager;
    private readonly Dictionary<string, int> _callCounts = new();
    private readonly string _staticCompetitionResponse;
    
    public Dictionary<string, int> CallCounts => _callCounts;
    
    /// <summary>
    /// Creates a handler with a Postman-based state manager.
    /// </summary>
    /// <param name="stateManager">The state manager initialized from a Postman collection</param>
    /// <param name="staticCompetitionResponse">Optional static competition JSON (if not provided, uses a minimal valid response)</param>
    public PostmanStateManagedHttpHandler(
        PostmanGameStateManager stateManager,
        string? staticCompetitionResponse = null)
    {
        _stateManager = stateManager;
        _staticCompetitionResponse = staticCompetitionResponse ?? GetMinimalCompetitionResponse();
    }
    
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        
        // Track call counts for verification
        var endpointType = GetEndpointType(url);
        if (!_callCounts.ContainsKey(endpointType))
        {
            _callCounts[endpointType] = 0;
        }
        _callCounts[endpointType]++;
        
        // Get appropriate response based on URL
        string content = url switch
        {
            var u when u.Contains("/status") => _stateManager.GetNextStatusResponse(),
            var u when u.Contains("/competitions/") && !u.Contains("/status") => _staticCompetitionResponse,
            _ => "{}"
        };
        
        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        });
    }
    
    private string GetEndpointType(string url)
    {
        return url switch
        {
            var u when u.Contains("/status") => "status",
            var u when u.Contains("/situation") => "situation",
            var u when u.Contains("/plays") => "plays",
            var u when u.Contains("/drives") => "drives",
            var u when u.Contains("/probabilities") => "probability",
            var u when u.Contains("/leaders") => "leaders",
            var u when u.Contains("/competitions/") => "competition",
            _ => "unknown"
        };
    }
    
    private static string GetMinimalCompetitionResponse()
    {
        // Minimal valid competition response with required child document refs
        return @"{
            ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846"",
            ""id"": ""401756846"",
            ""probabilities"": {
                ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846/probabilities""
            },
            ""drives"": {
                ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846/drives""
            },
            ""details"": {
                ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846/plays""
            },
            ""situation"": {
                ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846/situation""
            },
            ""leaders"": {
                ""$ref"": ""http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401756846/competitions/401756846/leaders""
            }
        }";
    }
}
