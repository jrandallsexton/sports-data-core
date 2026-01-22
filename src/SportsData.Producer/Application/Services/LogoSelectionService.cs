using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Services;

/// <summary>
/// Service for intelligently selecting logos optimized for different display contexts.
/// </summary>
public interface ILogoSelectionService
{
    /// <summary>
    /// Selects the best logo for display on a dark background by prioritizing manually curated logos,
    /// then falling back to Rel-based heuristics and avoiding white background variants.
    /// </summary>
    /// <param name="logos">Collection of logos to choose from</param>
    /// <returns>The URI of the selected logo, or null if no suitable logo exists</returns>
    Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos);
}

/// <summary>
/// Implementation of logo selection service that uses manual curation flags and
/// metadata-based heuristics to select appropriate logos for different display contexts.
/// </summary>
public class LogoSelectionService : ILogoSelectionService
{
    public Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos)
    {
        if (logos == null)
            return null;

        var logoList = logos.ToList();
        if (logoList.Count == 0)
            return null;

        // Priority 0: Manually curated logos marked as safe for dark backgrounds
        // If IsForDarkBg == true, this logo has been manually verified as safe
        var curatedDarkLogo = logoList.FirstOrDefault(l => l.IsForDarkBg == true);
        if (curatedDarkLogo != null)
            return curatedDarkLogo.Uri;

        // For all remaining logos (IsForDarkBg == null or false), fall back to Rel-based heuristics
        // The flag is opt-in only; false/null logos still participate in automatic selection

        // Priority 1: Logos explicitly designed for black backgrounds
        var blackLogo = logoList.FirstOrDefault(l => 
            l.Rel != null && l.Rel.Contains("primary_logo_on_black_color"));
        if (blackLogo != null)
            return blackLogo.Uri;

        // Priority 2: Logos marked as "dark"
        var darkLogo = logoList.FirstOrDefault(l => 
            l.Rel != null && l.Rel.Contains("dark"));
        if (darkLogo != null)
            return darkLogo.Uri;

        // Priority 3: Logos on team primary/secondary colors (usually safe)
        var teamColorLogo = logoList.FirstOrDefault(l => 
            l.Rel != null && (l.Rel.Contains("on_primary_color") || l.Rel.Contains("on_secondary_color")));
        if (teamColorLogo != null)
            return teamColorLogo.Uri;

        // Priority 4: Default logo
        var defaultLogo = logoList.FirstOrDefault(l => 
            l.Rel != null && l.Rel.Contains("default"));
        if (defaultLogo != null)
            return defaultLogo.Uri;

        // Priority 5: First logo that's NOT designed for white background
        var anyNonWhiteLogo = logoList.FirstOrDefault(l => 
            l.Rel == null || !l.Rel.Contains("on_white_color"));
        if (anyNonWhiteLogo != null)
            return anyNonWhiteLogo.Uri;

        // Absolute fallback
        return logoList.FirstOrDefault()?.Uri;
    }
}
