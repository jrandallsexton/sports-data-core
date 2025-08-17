using System;
using System.Globalization;

namespace SportsData.Core.Extensions
{
    public static class StringExtensions
    {
        public static string? ToCanonicalFormNullable(this string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            var trimmed = input.Trim().ToLowerInvariant();

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(trimmed);
        }

        public static string ToCanonicalForm(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentNullException(nameof(input), "Input cannot be null or empty.");
            }

            var trimmed = input.Trim().ToLowerInvariant();

            var textInfo = CultureInfo.InvariantCulture.TextInfo;
            return textInfo.ToTitleCase(trimmed);
        }

        public static int GetSizeInKilobytes(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            var byteCount = System.Text.Encoding.UTF8.GetByteCount(str);
            return byteCount / 1024;
        }

        public static DateTime? TryParseUtcNullable(this string? dateStr)
        {
            return DateTime.TryParse(dateStr, out var dt)
                ? dt.ToUniversalTime()
                : null;
        }
    }
}