#nullable enable
using FluentAssertions;

using SportsData.Core.Extensions;

using Xunit;

namespace SportsData.Core.Tests.Unit.Extensions
{
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData("   ", null)]

        [InlineData("place kicker", "Place Kicker")]
        [InlineData(" PLACE kicker ", "Place Kicker")]
        [InlineData("PLACE KICKER", "Place Kicker")]
        [InlineData("place KICKER", "Place Kicker")]
        [InlineData("  place    kicker  ", "Place    Kicker")] // preserves internal spaces

        [InlineData("long snapper", "Long Snapper")]
        [InlineData("LONG SNAPPER", "Long Snapper")]
        [InlineData(" Long Snapper ", "Long Snapper")]

        [InlineData("cornerback", "Cornerback")]
        [InlineData("CORNERBACK", "Cornerback")]
        [InlineData(" cornerback ", "Cornerback")]

        [InlineData("linebacker", "Linebacker")]
        [InlineData("LINEBACKER", "Linebacker")]
        [InlineData(" linebacker ", "Linebacker")]

        [InlineData("kicker/punter", "Kicker/Punter")]
        [InlineData(" KICKER/PUNTER ", "Kicker/Punter")]
        public void ToCanonicalForm_ReturnsExpected(string? input, string? expected)
        {
            var result = input.ToCanonicalFormNullable();
            result.Should().Be(expected);
        }
    }
}