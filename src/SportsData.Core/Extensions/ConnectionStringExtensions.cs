using System;

using Npgsql;

namespace SportsData.Core.Extensions
{
    public static class ConnectionStringExtensions
    {
        private const string Mask = "***";

        private const string UnparseableMessage =
            "(connection string could not be parsed; redacted in full)";

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
        ///
        /// Parsing uses NpgsqlConnectionStringBuilder rather than a regex:
        /// Npgsql accepts multiple password aliases (PWD, PSW) and quoted
        /// values containing semicolons or doubled quotes — hand-rolled
        /// matching leaks fragments on exactly those edges. If the string
        /// cannot be parsed, nothing of it is returned: we cannot prove
        /// which fragment is the secret.
        /// </remarks>
        public static string RedactCredentials(this string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);

                if (string.IsNullOrEmpty(builder.Password) &&
                    string.IsNullOrEmpty(builder.SslPassword))
                {
                    // Parsed and provably credential-free: preserve the
                    // original formatting exactly.
                    return connectionString;
                }

                if (!string.IsNullOrEmpty(builder.Password))
                    builder.Password = Mask;

                // Client-certificate key passphrase — a credential too.
                if (!string.IsNullOrEmpty(builder.SslPassword))
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
