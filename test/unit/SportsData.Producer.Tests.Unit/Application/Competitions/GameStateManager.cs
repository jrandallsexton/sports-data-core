using System.Text.Json;
using System.Net;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

/// <summary>
/// Manages game state progression for testing live game streaming.
/// Provides sequential responses that simulate a real game progressing from scheduled to final.
/// </summary>
public class GameStateManager
{
    private int _statusCallCount = 0;
    private readonly List<string> _statusResponses = new();
    private readonly Dictionary<string, List<string>> _documentResponses = new();
    private readonly string _gameDataPath;
    
    public int StatusCallCount => _statusCallCount;
    public string CurrentGameState { get; private set; } = "Scheduled";
    
    public GameStateManager(string gameDataPath)
    {
        _gameDataPath = gameDataPath;
        LoadAllResponses();
    }
    
    private void LoadAllResponses()
    {
        // Load status files (these drive the game progression)
        var statusFiles = Directory.GetFiles(_gameDataPath, "status-*.json")
            .OrderBy(f => f)
            .ToList();
        
        foreach (var file in statusFiles)
        {
            _statusResponses.Add(File.ReadAllText(file));
        }
        
        // Load other document types
        LoadDocumentType("situation");
        LoadDocumentType("plays");
        LoadDocumentType("drives");
        LoadDocumentType("probability");
        LoadDocumentType("leaders");
        
        // Load static documents (don't change during game)
        LoadStaticDocument("competition");
    }
    
    private void LoadDocumentType(string documentType)
    {
        var pattern = $"{documentType}-*.json";
        var files = Directory.GetFiles(_gameDataPath, pattern)
            .OrderBy(f => f)
            .ToList();
        
        if (files.Any())
        {
            _documentResponses[documentType] = files
                .Select(File.ReadAllText)
                .ToList();
        }
        else
        {
            // No files found - use empty list
            _documentResponses[documentType] = new List<string>();
        }
    }
    
    private void LoadStaticDocument(string documentType)
    {
        var file = Path.Combine(_gameDataPath, $"{documentType}.json");
        if (File.Exists(file))
        {
            _documentResponses[documentType] = new List<string> { File.ReadAllText(file) };
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
    /// Gets the current response for a document type based on game progression.
    /// Documents align with status progression.
    /// </summary>
    public string GetCurrentDocumentResponse(string documentType)
    {
        if (!_documentResponses.ContainsKey(documentType))
        {
            return "{}"; // Empty JSON if document type not found
        }
        
        var responses = _documentResponses[documentType];
        if (responses.Count == 0)
        {
            return "{}";
        }
        
        // For static documents (like competition), always return first
        if (responses.Count == 1)
        {
            return responses[0];
        }
        
        // For dynamic documents, align with status progression
        // Use current status index, clamped to available responses
        var index = Math.Min(_statusCallCount - 1, responses.Count - 1);
        index = Math.Max(0, index);
        
        return responses[index];
    }
    
    /// <summary>
    /// Resets the game state to the beginning.
    /// </summary>
    public void Reset()
    {
        _statusCallCount = 0;
        CurrentGameState = "Scheduled";
    }
    
    /// <summary>
    /// Gets metadata about loaded responses.
    /// </summary>
    public Dictionary<string, int> GetLoadedResponseCounts()
    {
        var counts = new Dictionary<string, int>
        {
            ["status"] = _statusResponses.Count
        };
        
        foreach (var kvp in _documentResponses)
        {
            counts[kvp.Key] = kvp.Value.Count;
        }
        
        return counts;
    }
}

/// <summary>
/// Custom HttpMessageHandler that uses GameStateManager to provide sequential responses.
/// Simulates ESPN API behavior during a live game.
/// </summary>
public class StateManagedHttpHandler : HttpMessageHandler
{
    private readonly GameStateManager _stateManager;
    private readonly Dictionary<string, int> _callCounts = new();
    
    public Dictionary<string, int> CallCounts => _callCounts;
    
    public StateManagedHttpHandler(GameStateManager stateManager)
    {
        _stateManager = stateManager;
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
            var u when u.Contains("/situation") => _stateManager.GetCurrentDocumentResponse("situation"),
            var u when u.Contains("/plays") => _stateManager.GetCurrentDocumentResponse("plays"),
            var u when u.Contains("/drives") => _stateManager.GetCurrentDocumentResponse("drives"),
            var u when u.Contains("/probabilities") => _stateManager.GetCurrentDocumentResponse("probability"),
            var u when u.Contains("/leaders") => _stateManager.GetCurrentDocumentResponse("leaders"),
            var u when u.Contains("/competitions/") && !u.Contains("/status") => 
                _stateManager.GetCurrentDocumentResponse("competition"),
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
}
