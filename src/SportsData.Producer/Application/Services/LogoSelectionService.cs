using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Services;

/// <summary>
/// Service for intelligently selecting logos optimized for different display contexts.
///
/// As of the team-mark rollout (2026-06), the selection cascade prefers sportDeets-generated
/// marks (Rel = "sportdeets-mark") over ESPN-sourced rows when present. The optional
/// <see cref="MarkDirection"/> parameter on each selection method picks the matching
/// direction (Roundel / Shield / Hex) when multiple sportdeets-mark rows exist for a team.
/// Direction defaults to <see cref="MarkDirection.Roundel"/> until the user-preference UI ships;
/// see docs/team-mark-user-preference-design.md for the larger plan.
/// </summary>
public interface ILogoSelectionService
{
    /// <summary>
    /// Selects the best logo for display on a dark background by prioritizing manually curated logos,
    /// then falling back to Rel-based heuristics and avoiding white background variants.
    /// </summary>
    Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>
    /// Selects the best logo for display on a light/default background by prioritizing manually curated logos,
    /// then falling back to Rel-based heuristics and preferring white background variants.
    /// </summary>
    Uri? SelectLogoForLightBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>
    /// Selects the best logo with fallback from season logos to franchise logos.
    /// Uses dark background selection by default.
    /// </summary>
    Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>
    /// Selects the best logo with fallback, for the specified background type.
    /// </summary>
    Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, bool darkBackground, MarkDirection direction = MarkDirection.Roundel);
}

/// <summary>
/// Implementation of logo selection service that uses manual curation flags and
/// metadata-based heuristics to select appropriate logos for different display contexts.
/// </summary>
public class LogoSelectionService : ILogoSelectionService
{
    // Rel-tag constants for sportDeets-generated marks. The marks batch script
    // writes rows with Rel = ["sportdeets-mark", "{direction}"]; see
    // src/marks/batch/upload.js and the team-mark design docs.
    private const string SportdeetsMarkRel = "sportdeets-mark";

    private static string DirectionRel(MarkDirection direction) =>
        direction.ToString().ToLowerInvariant();

    /// <summary>
    /// Picks a sportdeets-mark row. Prefers the row tagged with the requested
    /// direction; falls back to any sportdeets-mark row if the requested
    /// direction hasn't been generated for that team yet. Returns null when
    /// no sportdeets-mark rows exist — caller falls through to ESPN cascade.
    /// </summary>
    private static Uri? SelectSportdeetsMark(IReadOnlyList<ILogo> logos, MarkDirection direction)
    {
        var dirTag = DirectionRel(direction);
        var preferred = logos.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains(SportdeetsMarkRel) && l.Rel.Contains(dirTag));
        if (preferred != null)
            return preferred.Uri;

        var anyMark = logos.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains(SportdeetsMarkRel));
        return anyMark?.Uri;
    }

    public Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel)
    {
        if (logos == null)
            return null;

        var logoList = logos.ToList();
        if (logoList.Count == 0)
            return null;

        // Priority -1: sportdeets-generated mark for the requested direction
        // (or any sportdeets mark as fallback). When present, this wins over
        // all ESPN-sourced rows below. The marks are theme-agnostic, so the
        // same selection serves dark and light contexts.
        var sportdeetsMark = SelectSportdeetsMark(logoList, direction);
        if (sportdeetsMark != null)
            return sportdeetsMark;

        // Priority 0: Manually curated logos marked as safe for dark backgrounds
        var curatedDarkLogo = logoList.FirstOrDefault(l => l.IsForDarkBg == true);
        if (curatedDarkLogo != null)
            return curatedDarkLogo.Uri;

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

        // Priority 3: Logos on team primary/secondary colors (usually safe on dark)
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

    public Uri? SelectLogoForLightBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel)
    {
        if (logos == null)
            return null;

        var logoList = logos.ToList();
        if (logoList.Count == 0)
            return null;

        // Priority -1: sportdeets-generated mark (see SelectLogoForDarkBackground
        // for the rationale; the same Rel selection wins here because the
        // marks are theme-agnostic).
        var sportdeetsMark = SelectSportdeetsMark(logoList, direction);
        if (sportdeetsMark != null)
            return sportdeetsMark;

        // Priority 0: Manually curated — IsForDarkBg == false means curated for light
        var curatedLightLogo = logoList.FirstOrDefault(l => l.IsForDarkBg == false);
        if (curatedLightLogo != null)
            return curatedLightLogo.Uri;

        // Priority 1: Logos explicitly designed for white backgrounds
        var whiteLogo = logoList.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains("on_white_color"));
        if (whiteLogo != null)
            return whiteLogo.Uri;

        // Priority 2: Default logo
        var defaultLogo = logoList.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains("default"));
        if (defaultLogo != null)
            return defaultLogo.Uri;

        // Priority 3: Logos on team colors (generally safe)
        var teamColorLogo = logoList.FirstOrDefault(l =>
            l.Rel != null && (l.Rel.Contains("on_primary_color") || l.Rel.Contains("on_secondary_color")));
        if (teamColorLogo != null)
            return teamColorLogo.Uri;

        // Priority 4: First logo that's NOT designed for black background
        var anyNonDarkLogo = logoList.FirstOrDefault(l =>
            l.Rel == null || !l.Rel.Contains("primary_logo_on_black_color"));
        if (anyNonDarkLogo != null)
            return anyNonDarkLogo.Uri;

        // Absolute fallback
        return logoList.FirstOrDefault()?.Uri;
    }

    public Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, MarkDirection direction = MarkDirection.Roundel)
        => SelectWithFallback(seasonLogos, franchiseLogos, darkBackground: true, direction);

    public Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, bool darkBackground, MarkDirection direction = MarkDirection.Roundel)
    {
        var selector = darkBackground
            ? (Func<IEnumerable<ILogo>?, MarkDirection, Uri?>)SelectLogoForDarkBackground
            : SelectLogoForLightBackground;

        return selector(seasonLogos, direction) ?? selector(franchiseLogos, direction);
    }
}
