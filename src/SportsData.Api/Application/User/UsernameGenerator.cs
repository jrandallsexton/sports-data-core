namespace SportsData.Api.Application.User
{
    /// <summary>
    /// Builds a default username <i>seed</i> from a user's email (or display
    /// name as fallback). The seed is a normalized, length-bounded base; the
    /// caller resolves it to a unique value by appending the shortest numeric
    /// suffix that isn't taken (the suffix loop needs DB access, so it lives in
    /// the handler / backfill, not here). See docs/username-identity-foundation.md.
    /// </summary>
    public static class UsernameGenerator
    {
        private const string LastResortSeed = "user";

        /// <summary>
        /// Seed priority: sanitized email local-part → sanitized display name →
        /// "user". Local-part only, never the full email — the domain is the
        /// privacy/enumeration risk and the local-part alone can't reconstruct
        /// the address. Result is normalized and truncated to
        /// <see cref="UsernameNormalizer.MaxLength"/>; never shorter than
        /// <see cref="UsernameNormalizer.MinLength"/>.
        /// </summary>
        public static string BuildSeed(string? email, string? displayName)
        {
            var fromEmail = UsernameNormalizer.Normalize(LocalPart(email));
            if (fromEmail.Length >= UsernameNormalizer.MinLength)
                return Truncate(fromEmail);

            var fromDisplay = UsernameNormalizer.Normalize(displayName);
            if (fromDisplay.Length >= UsernameNormalizer.MinLength)
                return Truncate(fromDisplay);

            return LastResortSeed;
        }

        /// <summary>
        /// Appends <paramref name="suffix"/> to the seed, trimming the seed so
        /// the combined handle stays within <see cref="UsernameNormalizer.MaxLength"/>.
        /// </summary>
        public static string WithSuffix(string seed, int suffix)
        {
            var tag = suffix.ToString();
            var room = UsernameNormalizer.MaxLength - tag.Length;
            var trimmed = seed.Length > room ? seed[..room] : seed;
            return trimmed + tag;
        }

        // Local-part = text before '@', minus any '+tag' plus-addressing.
        private static string LocalPart(string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return string.Empty;

            var at = email.IndexOf('@');
            var local = at >= 0 ? email[..at] : email;

            var plus = local.IndexOf('+');
            return plus >= 0 ? local[..plus] : local;
        }

        private static string Truncate(string value) =>
            value.Length > UsernameNormalizer.MaxLength
                ? value[..UsernameNormalizer.MaxLength]
                : value;
    }
}
