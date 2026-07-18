using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Services;

/// <summary>
/// Selects team logos for display. FAIL-CLOSED (2026-07-18): only ever returns a
/// sportDeets-generated mark (a row whose <c>Rel</c> contains
/// <c>"sportdeets-mark"</c>), or <c>null</c>. It NEVER returns an ESPN-sourced /
/// untagged logo — those are licensed and must not render (app-store constraint;
/// see docs/logo-license-audit.md). A null result means the caller renders a
/// license-free placeholder.
///
/// <para>
/// The marks are theme-agnostic, so the dark/light methods select identically;
/// the <c>darkBackground</c> parameter is retained only for interface stability
/// and no longer affects the result. The optional <see cref="MarkDirection"/>
/// picks the matching direction (Roundel / Shield / Hex) when a team has multiple
/// marks; it defaults to <see cref="MarkDirection.Roundel"/>.
/// </para>
/// </summary>
public interface ILogoSelectionService
{
    /// <summary>Fail-closed: the team's mark for the given direction, or null.</summary>
    Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>Fail-closed: the team's mark for the given direction, or null.</summary>
    Uri? SelectLogoForLightBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>
    /// Fail-closed mark with season → franchise fallback: the season mark if one
    /// exists, else the franchise mark, else null. Uses dark selection by default
    /// (immaterial — marks are theme-agnostic).
    /// </summary>
    Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, MarkDirection direction = MarkDirection.Roundel);

    /// <summary>
    /// Fail-closed mark with season → franchise fallback. <paramref name="darkBackground"/>
    /// is retained for interface stability and no longer affects the result.
    /// </summary>
    Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, bool darkBackground, MarkDirection direction = MarkDirection.Roundel);
}

/// <inheritdoc />
public class LogoSelectionService : ILogoSelectionService
{
    // The marks batch (src/marks/batch/upload.js) writes rows with
    // Rel = ["sportdeets-mark", "{direction}"]. An untagged / ESPN-sourced row
    // has no such tag and is therefore never selected.
    private const string SportdeetsMarkRel = "sportdeets-mark";

    private static string DirectionRel(MarkDirection direction) =>
        direction.ToString().ToLowerInvariant();

    /// <summary>
    /// The single selection primitive: returns ONLY a sportdeets-mark Uri —
    /// preferring the requested direction, falling back to any mark — or null
    /// when the team has no mark at all. This is what makes the service
    /// fail-closed: an ESPN / untagged row can never be returned.
    /// </summary>
    private static Uri? SelectMark(IEnumerable<ILogo>? logos, MarkDirection direction)
    {
        if (logos == null)
            return null;

        var list = logos as IReadOnlyList<ILogo> ?? logos.ToList();
        if (list.Count == 0)
            return null;

        var dirTag = DirectionRel(direction);
        var preferred = list.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains(SportdeetsMarkRel) && l.Rel.Contains(dirTag));
        if (preferred != null)
            return preferred.Uri;

        var anyMark = list.FirstOrDefault(l =>
            l.Rel != null && l.Rel.Contains(SportdeetsMarkRel));
        return anyMark?.Uri;
    }

    public Uri? SelectLogoForDarkBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel)
        => SelectMark(logos, direction);

    public Uri? SelectLogoForLightBackground(IEnumerable<ILogo>? logos, MarkDirection direction = MarkDirection.Roundel)
        => SelectMark(logos, direction);

    public Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, MarkDirection direction = MarkDirection.Roundel)
        => SelectWithFallback(seasonLogos, franchiseLogos, darkBackground: true, direction);

    public Uri? SelectWithFallback(IEnumerable<ILogo>? seasonLogos, IEnumerable<ILogo>? franchiseLogos, bool darkBackground, MarkDirection direction = MarkDirection.Roundel)
        // Season mark preferred, franchise mark fallback, else null. Because
        // SelectMark returns null for a mark-less (ESPN-only) season, the franchise
        // mark is now correctly reached — the prior fail-open shadowing bug
        // (a season ESPN logo hiding a franchise mark) is gone.
        => SelectMark(seasonLogos, direction) ?? SelectMark(franchiseLogos, direction);
}
