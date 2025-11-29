using Microsoft.Extensions.Options;

namespace SportsData.Api.Config;

/// <summary>
/// Singleton service that provides access to synthetic user pick style configurations.
/// Loaded once at startup from Azure App Configuration.
/// </summary>
public class SyntheticPickStyleProvider : ISyntheticPickStyleProvider
{
    private readonly SyntheticUserPickStylesConfig _config;
    private readonly ILogger<SyntheticPickStyleProvider> _logger;

    public SyntheticPickStyleProvider(
        IOptions<SyntheticUserPickStylesConfig> options,
        ILogger<SyntheticPickStyleProvider> logger)
    {
        _config = options.Value;
        _logger = logger;
        
        _logger.LogInformation(
            "SyntheticPickStyleProvider initialized with {Count} pick styles: {Styles}",
            _config.Count,
            string.Join(", ", _config.Keys));
    }

    public SyntheticUserPickStyle GetPickStyle(string styleName)
    {
        if (string.IsNullOrWhiteSpace(styleName))
        {
            throw new ArgumentException("Style name cannot be null or empty", nameof(styleName));
        }

        if (!_config.TryGetValue(styleName.ToLower().Trim(), out var style))
        {
            var availableStyles = string.Join(", ", _config.Keys);
            throw new ArgumentException(
                $"Pick style '{styleName}' not found. Available styles: {availableStyles}",
                nameof(styleName));
        }

        return style;
    }

    public double GetRequiredConfidence(string styleName, double spreadAbs)
    {
        if (spreadAbs < 0)
        {
            throw new ArgumentException("Spread must be a positive value (absolute)", nameof(spreadAbs));
        }

        var style = GetPickStyle(styleName);

        // Iterate through thresholds in order to find the matching tier
        foreach (var threshold in style.Thresholds)
        {
            // If maxSpread is null, this is the catch-all tier for large spreads
            if (threshold.MaxSpread == null || spreadAbs < threshold.MaxSpread.Value)
            {
                _logger.LogDebug(
                    "Spread {Spread} for style '{Style}' requires confidence >= {Confidence}",
                    spreadAbs,
                    styleName,
                    threshold.MinConfidence);
                    
                return threshold.MinConfidence;
            }
        }

        // Fallback: return the last threshold's confidence (shouldn't reach here if config is valid)
        var fallbackConfidence = style.Thresholds.Last().MinConfidence;
        _logger.LogWarning(
            "No matching threshold found for spread {Spread} in style '{Style}', using fallback: {Confidence}",
            spreadAbs,
            styleName,
            fallbackConfidence);
            
        return fallbackConfidence;
    }
}
