using System.Text.RegularExpressions;

namespace SportsData.Api.Application.User
{
    /// <summary>
    /// Username rules + normalization, shared by the new-user mint
    /// (<see cref="Commands.UpsertUser.UpsertUserCommandHandler"/>), the
    /// edit/validate path, and the backfill. Usernames are stored
    /// already-lowercased so the unique index gives case-insensitive
    /// uniqueness for free. See docs/username-identity-foundation.md.
    /// </summary>
    public static partial class UsernameNormalizer
    {
        public const int MinLength = 3;
        public const int MaxLength = 30;

        // Handles that would enable impersonation or collide with routes.
        private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "administrator", "api", "root", "support", "help",
            "sportdeets", "system", "moderator", "mod", "null", "undefined"
        };

        [GeneratedRegex("[^a-z0-9_]")]
        private static partial Regex InvalidChars();

        /// <summary>
        /// Lowercases and strips any character outside <c>[a-z0-9_]</c>. Does NOT
        /// enforce length — callers decide how to handle a too-short result
        /// (the generator falls back to another seed; validation rejects it).
        /// </summary>
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            return InvalidChars().Replace(raw.Trim().ToLowerInvariant(), string.Empty);
        }

        public static bool IsReserved(string normalized) => Reserved.Contains(normalized);

        /// <summary>
        /// True when <paramref name="raw"/> normalizes to a valid handle: the
        /// normalized form is unchanged (no illegal chars were dropped), within
        /// length bounds, and not reserved.
        /// </summary>
        public static bool IsValid(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var trimmed = raw.Trim();
            var normalized = Normalize(trimmed);

            // Reject if normalization had to change anything (spaces, caps,
            // punctuation) — callers must submit the already-clean handle.
            if (!string.Equals(normalized, trimmed.ToLowerInvariant(), StringComparison.Ordinal))
                return false;

            return normalized.Length is >= MinLength and <= MaxLength
                && !IsReserved(normalized);
        }
    }
}
