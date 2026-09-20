using System.Text.Json;

using Microsoft.Extensions.Configuration;

namespace SportsData.Api.Config;

/// <summary>
/// Configuration for synthetic user pick styles loaded from Azure App Configuration.
/// Key: SportsData.Api:syntheticUserPickStyles
/// Binds directly to the nested dictionary in the JSON:
/// {
///   "syntheticUserPickStyles": {
///     "moderate": { "name": "Moderate", "description": "...", "thresholds": [...] },
///     "conservative": { ... },
///     "aggressive": { ... }
///   }
/// }
/// </summary>
public class SyntheticUserPickStylesConfig : Dictionary<string, SyntheticUserPickStyle>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Populates this config from the RAW JSON held at <paramref name="key"/>,
    /// rather than letting the section binder walk it.
    /// </summary>
    /// <remarks>
    /// The AppConfig value is one key holding a nested document. Azure only
    /// expands such a value into hierarchical config keys when the setting
    /// carries content-type <c>application/json</c>, and the provisioning
    /// script preserves content types for Key Vault references only — every
    /// other entry goes through <c>az appconfig kv import</c>, which drops it.
    /// The section therefore has a VALUE but no CHILDREN, and binding a
    /// dictionary from a childless section yields an empty one.
    /// <para>
    /// That failed silently for a long time: the provider logged "0 pick
    /// styles" at startup and nothing consulted it until an ATS matchup with a
    /// spread came along, at which point every styled bot threw
    /// ArgumentException mid-sweep. Straight-up leagues and spreadless ATS
    /// matchups never reach the style lookup, which is why picks kept being
    /// produced and the gap stayed invisible.
    /// </para>
    /// <para>
    /// Deliberately tolerant: a missing or unparseable value yields an EMPTY
    /// config rather than throwing at startup. The caller decides what an
    /// absent style means; see <c>SyntheticPickStyleProvider</c>, which logs
    /// the count so an empty load is visible rather than inferred.
    /// </para>
    /// </remarks>
    public static Action<SyntheticUserPickStylesConfig> BindFrom(IConfiguration configuration, string key)
    {
        var raw = configuration[key];

        return target =>
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var parsed = JsonSerializer.Deserialize<Dictionary<string, SyntheticUserPickStyle>>(raw, JsonOptions);
            if (parsed is null)
                return;

            foreach (var (styleName, style) in parsed)
            {
                // Lookups lowercase the name (GetPickStyle), and the stored
                // keys already are — normalise anyway so a capitalised key in
                // AppConfig cannot reintroduce a silent miss.
                target[styleName.ToLowerInvariant().Trim()] = style;
            }
        };
    }
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
