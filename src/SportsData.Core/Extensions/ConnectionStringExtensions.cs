using System;
using System.Text.RegularExpressions;

namespace SportsData.Core.Extensions
{
    public static class ConnectionStringExtensions
    {
        // Npgsql accepts several spellings for the credential keys; match
        // them all, case-insensitively, up to the next ';' or end of string.
        private static readonly Regex SecretKeys = new(
            @"\b(Password|Pwd)\s*=\s*[^;]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Masks credentials in a connection string so it can be logged.
        /// Everything operationally useful — host, port, database, pool
        /// size, application name — survives; only the secret is replaced.
        /// </summary>
        /// <remarks>
        /// Startup logs ship to Seq, so an unredacted connection string
        /// puts the production database password in front of anyone with
        /// log access (and in any log export or screenshot). Always call
        /// this before writing a connection string anywhere.
        /// </remarks>
        public static string RedactCredentials(this string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            return SecretKeys.Replace(connectionString, match =>
            {
                var key = match.Value[..match.Value.IndexOf('=')].TrimEnd();
                return $"{key}=***";
            });
        }
    }
}
