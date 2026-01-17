#nullable enable

using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

using SportsData.Core.Extensions;

namespace SportsData.Core.Tests.Unit.Extensions;

public class DbUpdateExceptionExtensionsTests
{
    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnTrue_WhenPostgreSqlUniqueViolation()
    {
        // Arrange - Since PostgresException constructor is complex, rely on message-based detection
        var innerException = new Exception("duplicate key value violates unique constraint (SqlState: 23505)");
        var dbUpdateException = new DbUpdateException("Db update failed", innerException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip="due to nuget upgrade")]
    public void IsUniqueConstraintViolation_ShouldReturnTrue_WhenSqlServerUniqueViolation2601()
    {
        // Arrange
        var sqlException = CreateSqlException(2601);
        var dbUpdateException = new DbUpdateException("Db update failed", sqlException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeTrue();
    }

    [Fact(Skip = "due to nuget upgrade")]
    public void IsUniqueConstraintViolation_ShouldReturnTrue_WhenSqlServerUniqueViolation2627()
    {
        // Arrange
        var sqlException = CreateSqlException(2627);
        var dbUpdateException = new DbUpdateException("Db update failed", sqlException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnTrue_WhenMessageContainsDuplicateKey()
    {
        // Arrange - Generic exception with "duplicate key" in message
        var innerException = new Exception("Error: duplicate key value violates unique constraint");
        var dbUpdateException = new DbUpdateException("Db update failed", innerException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnTrue_WhenMessageContainsUniqueConstraint()
    {
        // Arrange - Generic exception with "unique constraint" in message
        var innerException = new Exception("Cannot insert duplicate value, unique constraint violation");
        var dbUpdateException = new DbUpdateException("Db update failed", innerException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnFalse_WhenNoInnerException()
    {
        // Arrange
        var dbUpdateException = new DbUpdateException("Db update failed");

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnFalse_WhenExceptionIsNull()
    {
        // Arrange
        DbUpdateException? dbUpdateException = null;

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Skip = "due to nuget upgrade")]
    public void IsUniqueConstraintViolation_ShouldReturnFalse_WhenDifferentSqlErrorNumber()
    {
        // Arrange - SQL error 547 is for foreign key constraint, not unique
        var sqlException = CreateSqlException(547);
        var dbUpdateException = new DbUpdateException("Db update failed", sqlException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnFalse_WhenPostgresDifferentSqlState()
    {
        // Arrange - PostgreSQL error 23503 is for foreign key violation, not unique
        var innerException = new Exception("foreign key violation (SqlState: 23503)");
        var dbUpdateException = new DbUpdateException("Db update failed", innerException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsUniqueConstraintViolation_ShouldReturnFalse_WhenGenericException()
    {
        // Arrange - Generic exception without constraint violation keywords
        var innerException = new Exception("Some other database error occurred");
        var dbUpdateException = new DbUpdateException("Db update failed", innerException);

        // Act
        var result = dbUpdateException.IsUniqueConstraintViolation();

        // Assert
        result.Should().BeFalse();
    }

    /// <summary>
    /// Helper method to create a SqlException with a specific error number.
    /// SqlException doesn't have a public constructor, so we use reflection.
    /// </summary>
    private static SqlException CreateSqlException(int errorNumber)
    {
        // Create a SqlError using reflection
        var sqlErrorType = typeof(SqlError);
        var sqlErrorConstructor = sqlErrorType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string), typeof(string), typeof(int), typeof(uint), typeof(Exception) },
            null);

        if (sqlErrorConstructor == null)
            throw new Exception("Could not find SqlError constructor");

        var sqlError = sqlErrorConstructor.Invoke(new object?[]
        {
            errorNumber,  // number
            (byte)0,      // state
            (byte)0,      // errorClass
            "server",     // server
            "error",      // message
            "proc",       // procedure
            0,            // lineNumber
            (uint)0,      // win32ErrorCode
            null          // inner exception
        }) as SqlError;

        // Create SqlErrorCollection and add the error
        var sqlErrorCollectionType = typeof(SqlErrorCollection);
        var sqlErrorCollectionConstructor = sqlErrorCollectionType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (sqlErrorCollectionConstructor == null)
            throw new Exception("Could not find SqlErrorCollection constructor");

        var sqlErrorCollection = sqlErrorCollectionConstructor.Invoke(null) as SqlErrorCollection;

        if (sqlError != null && sqlErrorCollection != null)
        {
            var addMethod = sqlErrorCollectionType.GetMethod("Add",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            addMethod?.Invoke(sqlErrorCollection, new object[] { sqlError });
        }

        // Create SqlException using the error collection
        var sqlExceptionType = typeof(SqlException);
        var sqlExceptionConstructor = sqlExceptionType.GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            new[] { typeof(string), typeof(SqlErrorCollection), typeof(Exception), typeof(Guid) },
            null);

        if (sqlExceptionConstructor == null)
            throw new Exception("Could not find SqlException constructor");

        var sqlException = sqlExceptionConstructor.Invoke(new object?[]
        {
            "SQL Error occurred",
            sqlErrorCollection,
            null,
            Guid.Empty
        }) as SqlException;

        return sqlException ?? throw new Exception("Failed to create SqlException");
    }
}
