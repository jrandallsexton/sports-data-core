using SportsData.Core.Infrastructure.Blobs;

namespace SportsData.Api.Infrastructure.Prompts;

public class MatchupPreviewPromptProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly object _lock = new();

    private Task<(string, string)>? _cachedPromptTask;
    private Task<(string, string)>? _cachedPromptWithStatsTask;

    private const string Container = "prompts";

    private const string Blob = "prediction-insights-v1.txt";
    private const string BlobWithStats = "prediction-insights-with-stats-schedule.txt";

    public MatchupPreviewPromptProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task<(string PromptText, string PromptName)> GetPreviewInsightPromptAsync(bool hasStats)
    {
        // Evict faulted tasks so a transient blob failure doesn't poison the
        // cache until process restart — every later call would receive the
        // same faulted task and the whole preview pipeline stays down.
        lock (_lock)
        {
            if (hasStats)
            {
                if (_cachedPromptWithStatsTask is { IsFaulted: true } or { IsCanceled: true })
                    _cachedPromptWithStatsTask = null;
                return _cachedPromptWithStatsTask ??= LoadPromptAsync(BlobWithStats);
            }
            else
            {
                if (_cachedPromptTask is { IsFaulted: true } or { IsCanceled: true })
                    _cachedPromptTask = null;
                return _cachedPromptTask ??= LoadPromptAsync(Blob);
            }
        }
    }

    public async Task<string> ReloadPromptAsync(bool hasStats)
    {
        var blobName = hasStats ? BlobWithStats : Blob;
        var newPrompt = await LoadPromptTextOnlyAsync(blobName);

        lock (_lock)
        {
            if (hasStats)
                _cachedPromptWithStatsTask = Task.FromResult((newPrompt, Path.GetFileNameWithoutExtension(blobName)));
            else
                _cachedPromptTask = Task.FromResult((newPrompt, Path.GetFileNameWithoutExtension(blobName)));
        }

        return newPrompt;
    }

    private async Task<(string, string)> LoadPromptAsync(string blobName, bool forceReload = false)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        var promptText = await blobStorage.GetFileContentsAsync(Container, blobName);
        var promptName = Path.GetFileNameWithoutExtension(blobName);

        return (promptText, promptName);
    }

    private async Task<string> LoadPromptTextOnlyAsync(string blobName)
    {
        using var scope = _serviceProvider.CreateScope();
        var blobStorage = scope.ServiceProvider.GetRequiredService<IProvideBlobStorage>();

        return await blobStorage.GetFileContentsAsync(Container, blobName);
    }
}
