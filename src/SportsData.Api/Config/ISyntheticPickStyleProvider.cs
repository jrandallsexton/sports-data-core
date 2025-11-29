namespace SportsData.Api.Config;

/// <summary>
/// Provides access to synthetic user pick style configurations.
/// </summary>
public interface ISyntheticPickStyleProvider
{
    /// <summary>
    /// Gets the pick style configuration by name.
    /// </summary>
    /// <param name="styleName">The name of the pick style (e.g., "moderate", "conservative", "aggressive")</param>
    /// <returns>The pick style configuration</returns>
    /// <exception cref="ArgumentException">Thrown when the style name is not found</exception>
    SyntheticUserPickStyle GetPickStyle(string styleName);
    
    /// <summary>
    /// Gets the required confidence threshold for a given pick style and spread.
    /// </summary>
    /// <param name="styleName">The name of the pick style</param>
    /// <param name="spreadAbs">The absolute value of the spread</param>
    /// <returns>The minimum confidence required (0.0 to 1.0)</returns>
    double GetRequiredConfidence(string styleName, double spreadAbs);
}
