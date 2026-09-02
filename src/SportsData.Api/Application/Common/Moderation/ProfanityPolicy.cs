using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SportsData.Api.Application.Common.Moderation;

/// <summary>
/// Profanity check for user-authored labels (display names, league names).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Whole-word matching ONLY — never substring.</strong> These are
/// names, and substring matching is the classic Scunthorpe problem: it blocks
/// Sexton, Dickinson, Cummings, Hancock — real surnames, including the
/// operator's. A candidate is rejected only when a normalized TOKEN equals a
/// banned word, or the entire input collapsed to letters equals one (which
/// catches spacing/punctuation evasion like "f.u.c.k").
/// </para>
/// <para>
/// Normalization before matching: lowercase, diacritics stripped, common
/// leet substitutions folded (0→o, 1→i, 3→e, 4→a, 5→s, 7→t, $→s, @→a, !→i),
/// then split on every non-letter. "S3xt0n" therefore normalizes to "sexton"
/// and still passes — the guarantee comes from whole-word equality, not from
/// the normalizer being conservative.
/// </para>
/// <para>
/// The wordlist is an embedded resource next to this file — one word per
/// line, '#' comments allowed. It is deliberately curated for the NAME
/// context (precision over recall): a miss is a moderation follow-up, a
/// false positive blocks a real person's actual name. This filter is one
/// layer; the operator can always remove content server-side.
/// </para>
/// </remarks>
public static class ProfanityPolicy
{
    private static readonly Lazy<HashSet<string>> BannedWords = new(LoadWordList);

    /// <summary>
    /// True when the candidate contains a banned word as a whole token, or
    /// collapses (letters-only) to one. Null/whitespace is not profane —
    /// required-ness is a separate validation concern.
    /// </summary>
    public static bool ContainsProfanity(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var normalized = Normalize(candidate);
        var banned = BannedWords.Value;

        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (banned.Contains(token))
                return true;
        }

        var collapsed = normalized.Replace(" ", string.Empty);
        return banned.Contains(collapsed);
    }

    /// <summary>
    /// Substitution form for PROVISIONING paths (signup / federated login):
    /// returns null when the candidate is profane so the caller's existing
    /// null-fallbacks take over (generated name at create, keep-current at
    /// update). These paths must never REJECT — account creation cannot be
    /// allowed to fail over a Google profile name — which is why this exists
    /// alongside the rejecting validator rules used on deliberate renames.
    /// </summary>
    public static string? SanitizeOrNull(string? candidate) =>
        ContainsProfanity(candidate) ? null : candidate;

    /// <summary>
    /// Lowercase, strip diacritics, fold leet substitutions, and replace every
    /// remaining non-letter with a space (token boundary).
    /// </summary>
    private static string Normalize(string value)
    {
        // FormKD, not FormD: canonical decomposition alone handles diacritics
        // but leaves COMPATIBILITY characters intact, so full-width letters
        // (e.g. “ｆｕｃｋ”) sailed through as non-a-z and matched nothing.
        // Compatibility decomposition folds them to ASCII before matching.
        var lowered = value.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(lowered.Length);

        foreach (var ch in lowered)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue; // diacritic — drop it (é→e came from the FormKD decomposition)

            var folded = ch switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '$' => 's',
                '@' => 'a',
                '!' => 'i',
                _ => ch
            };

            sb.Append(folded is >= 'a' and <= 'z' ? folded : ' ');
        }

        return sb.ToString();
    }

    private static HashSet<string> LoadWordList()
    {
        var assembly = typeof(ProfanityPolicy).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("profanity-wordlist.txt", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "Embedded resource profanity-wordlist.txt not found — check the csproj EmbeddedResource entry.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);

        var words = new HashSet<string>(StringComparer.Ordinal);
        while (reader.ReadLine() is { } line)
        {
            var word = line.Trim();
            if (word.Length == 0 || word.StartsWith('#'))
                continue;
            words.Add(word.ToLowerInvariant());
        }

        return words;
    }
}
