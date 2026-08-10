using System;
using System.Text.RegularExpressions;

using Npgsql;

namespace SportsData.Core.Extensions
{
    public static class ConnectionStringExtensions
    {
        private const string Mask = "***";

        private const string UnparseableMessage =
            "(connection string could not be parsed; redacted in full)";

        // Key-presence probes against the RAW text. The builder cannot be
        // the gate: duplicate aliases resolve last-wins and empty values
        // are dropped from its collection, so "Password=secret;PWD=" looks
        // credential-free to the builder while the secret still sits in
        // the original string. These match KEYS only, never values.
        private static readonly Regex PasswordKeyProbe = new(
            @"(^|;)\s*(password|pwd|psw)\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SslPasswordKeyProbe = new(
            @"(^|;)\s*ssl\s*password\s*=",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Masks credentials in a connection string so it can be logged.
        /// Everything operationally useful — host, port, database, pool
        /// size, application name — survives; only secrets are replaced.
        /// </summary>
        /// <remarks>
        /// Startup logs ship to Seq, so an unredacted connection string
        /// puts the production database password in front of anyone with
        /// log access (and in any log export or screenshot). Always call
        /// this before writing a connection string anywhere.
        ///
        /// Strategy: a raw-text probe decides whether credential keys are
        /// present at all (verbatim passthrough only when provably none),
        /// then NpgsqlConnectionStringBuilder re-renders the string with
        /// the credential properties masked — the rebuild discards every
        /// original occurrence, including duplicate aliases. A string that
        /// has credential keys but cannot be parsed returns a fixed
        /// message: we cannot prove which fragment is the secret.
        /// </remarks>
        public static string RedactCredentials(this string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            var hasPasswordKey = PasswordKeyProbe.IsMatch(connectionString);
            var hasSslPasswordKey = SslPasswordKeyProbe.IsMatch(connectionString);

            if (!hasPasswordKey && !hasSslPasswordKey)
            {
                // Provably credential-free: preserve the original
                // formatting exactly.
                return connectionString;
            }

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);

                if (hasPasswordKey)
                    builder.Password = Mask;

                // Client-certificate key passphrase — a credential too.
                if (hasSslPasswordKey)
                    builder.SslPassword = Mask;

                return builder.ConnectionString;
            }
            catch (Exception)
            {
                return UnparseableMessage;
            }
        }
    }
}
