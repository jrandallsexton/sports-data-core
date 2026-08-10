using FluentAssertions;

using SportsData.Core.Extensions;

using Xunit;

namespace SportsData.Core.Tests.Unit.Extensions
{
    public class ConnectionStringExtensionsTests
    {
        [Fact]
        public void RedactCredentials_MasksPassword_AndKeepsDiagnostics()
        {
            const string connString =
                "Host=db.example.invalid;Port=5432;Username=example-user;Password=fixture-password;" +
                "Database=ExampleDb;Maximum Pool Size=5;Application Name=Example.Api.Data;";

            var redacted = connString.RedactCredentials();

            redacted.Should().NotContain("fixture-password");
            redacted.Should().Contain("Password=***");

            // Everything an operator actually needs at startup survives.
            redacted.Should().Contain("Host=db.example.invalid");
            redacted.Should().Contain("Port=5432");
            redacted.Should().Contain("Username=example-user");
            redacted.Should().Contain("Database=ExampleDb");
            redacted.Should().Contain("Maximum Pool Size=5");
            redacted.Should().Contain("Application Name=Example.Api.Data");
        }

        [Theory]
        [InlineData("Host=h;Password=secret;Database=d")]
        [InlineData("Host=h;password=secret;Database=d")]
        [InlineData("Host=h;PASSWORD=secret;Database=d")]
        [InlineData("Host=h;Pwd=secret;Database=d")]
        [InlineData("Host=h;PSW=secret;Database=d")]
        [InlineData("Host=h;Password = secret;Database=d")]
        public void RedactCredentials_HandlesKeySpellingsAndCasing(string connString)
        {
            connString.RedactCredentials().Should().NotContain("secret");
        }

        [Theory]
        [InlineData("Host=h;Password='first;second';Database=d")]
        [InlineData("Host=h;Password=\"first;second\";Database=d")]
        public void RedactCredentials_MasksQuotedValuesContainingSemicolons(string connString)
        {
            // Npgsql allows semicolons inside quoted values; a naive
            // "up to the next semicolon" match would leak the tail.
            var redacted = connString.RedactCredentials();

            redacted.Should().NotContain("first");
            redacted.Should().NotContain("second");
            redacted.Should().Contain("Database=d");
        }

        [Fact]
        public void RedactCredentials_MasksSslPassword()
        {
            // The client-certificate key passphrase is a credential too.
            var redacted = "Host=h;SSL Password=cert-secret;Database=d".RedactCredentials();

            redacted.Should().NotContain("cert-secret");
            redacted.Should().Contain("Database=d");
        }

        [Fact]
        public void RedactCredentials_MasksDoubledQuotesInsideQuotedValue()
        {
            var redacted = "Host=h;Password='fragment''tail;x';Database=d".RedactCredentials();

            redacted.Should().NotContain("fragment");
            redacted.Should().NotContain("tail");
        }

        [Fact]
        public void RedactCredentials_UnparseableStringExposesNothing()
        {
            // Unclosed quote: parsing fails, and we cannot prove which
            // fragment is the secret — so none of the input comes back.
            var redacted = "Host=h;Password='fragment;Database=d".RedactCredentials();

            redacted.Should().NotContain("fragment");
            redacted.Should().NotContain("Host");
            redacted.Should().Contain("redacted in full");
        }

        [Fact]
        public void RedactCredentials_MasksPasswordAtEndOfString()
        {
            "Host=h;Database=d;Password=secret".RedactCredentials()
                .Should().Be("Host=h;Database=d;Password=***");
        }

        [Fact]
        public void RedactCredentials_LeavesCredentialFreeStringsAlone()
        {
            const string connString = "Host=h;Port=5432;Database=d";
            connString.RedactCredentials().Should().Be(connString);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RedactCredentials_HandlesEmptyInput(string connString)
        {
            connString.RedactCredentials().Should().BeEmpty();
        }
    }
}
