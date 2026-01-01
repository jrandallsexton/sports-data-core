using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SportsData.Core.Extensions
{
    public static class DbUpdateExceptionExtensions
    {
        /// <summary>
        /// Determines if a DbUpdateException is caused by a unique constraint violation.
        /// Supports PostgreSQL, SQL Server, and fallback string-based detection.
        /// </summary>
        /// <param name="exception">The DbUpdateException to check</param>
        /// <returns>True if the exception is caused by a unique constraint violation; otherwise false</returns>
        public static bool IsUniqueConstraintViolation(this DbUpdateException? exception)
        {
            if (exception?.InnerException == null)
                return false;

            // PostgreSQL: Check for PostgresException with SqlState 23505 (unique_violation)
            if (exception.InnerException is PostgresException pgEx)
            {
                return pgEx.SqlState == "23505";
            }

            // SQL Server: Check for SqlException with error numbers 2601 or 2627
            if (exception.InnerException is SqlException sqlEx)
            {
                return sqlEx.Number == 2601 || sqlEx.Number == 2627;
            }

            // Fallback: String-based check for generic "duplicate key" or "unique constraint" messages
            var message = exception.InnerException.Message;
            if (!string.IsNullOrEmpty(message))
            {
                return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                       message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
