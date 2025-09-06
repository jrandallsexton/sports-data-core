using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();

    private Task<string>? _cachedPromptTask;
    private Task<string>? _cachedPromptWithStatsTask;

    private const string Container = "prompts";

    private const string Blob = "prediction-insights-v1.txt";
    private const string BlobWithStats = "prediction-insights-with-stats.txt";

    public MatchupPreviewPromptProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<string> GetPreviewInsightPromptAsync(bool hasStats)
    {
        if (hasStats)
        {
            if (_cachedPromptWithStatsTask != null)
                return _cachedPromptWithStatsTask;
        }
        else
        {
            if (_cachedPromptTask != null)
                return _cachedPromptTask;
        }

        lock (_lock)
        {
            if (hasStats)
            {
                return _cachedPromptWithStatsTask ??= LoadPromptAsync(BlobWithStats);
            }
            else
            {
                return _cachedPromptTask ??= LoadPromptAsync(Blob);
            }
        }
    }

    public async Task<string> ReloadPromptAsync(bool hasStats)
    {
        var blobName = hasStats ? BlobWithStats : Blob;
        var newPrompt = await LoadPromptAsync(blobName, forceReload: true);

        lock (_lock)
        {
            if (hasStats)
                _cachedPromptWithStatsTask = Task.FromResult(newPrompt);
            else
                _cachedPromptTask = Task.FromResult(newPrompt);
        }

        return newPrompt;
    }

    private async Task<string> LoadPromptAsync(string blobName, bool forceReload = false)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        return await blobStorage.GetFileContentsAsync(Container, blobName);
    }
}
