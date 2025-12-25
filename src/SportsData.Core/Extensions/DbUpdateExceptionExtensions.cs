using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SportsData.Core.Extensions
{
    public static class DbUpdateExceptionExtensions
    {
        public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
        {
            return exception.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
        }
    }
}
