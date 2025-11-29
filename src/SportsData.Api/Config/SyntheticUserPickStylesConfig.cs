namespace SportsData.Api.Config;

/// <summary>
/// Configuration for synthetic user pick styles loaded from Azure App Configuration.
/// Key: SportsData.Api:SyntheticUserPickStyles
/// </summary>
public class SyntheticUserPickStylesConfig
{
    public Dictionary<string, SyntheticUserPickStyle> SyntheticUserPickStyles { get; set; } = new();
}

public class SyntheticUserPickStyle
{
    public string Name { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public List<ConfidenceThreshold> Thresholds { get; set; } = new();
}

public class ConfidenceThreshold
{
    /// <summary>
    /// The maximum spread value for this threshold tier.
    /// Null represents "all spreads >= previous tier" (catch-all).
    /// </summary>
    public double? MaxSpread { get; set; }
    
    /// <summary>
    /// The minimum confidence required to pick the favorite at this spread level.
    /// Value between 0.0 and 1.0 (e.g., 0.70 = 70%).
    /// </summary>
    public double MinConfidence { get; set; }
}
