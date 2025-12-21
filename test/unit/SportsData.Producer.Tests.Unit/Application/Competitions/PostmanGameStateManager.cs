using System.Net;
using System.Text.Json;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

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
        var doc = JsonDocument.Parse(json);
        
        // Find the Status request in the collection
        var items = doc.RootElement.GetProperty("item");
        
        JsonElement? statusItem = null;
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
            var doc = JsonDocument.Parse(statusJson);
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
