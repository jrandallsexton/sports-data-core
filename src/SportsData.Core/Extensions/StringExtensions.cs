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

        /// <summary>
        /// Gets the size of the string in kilobytes (KB) using UTF-8 encoding.
        /// Uses ceiling to ensure size is not under-reported due to integer truncation.
        /// </summary>
        public static int GetSizeInKilobytes(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            var byteCount = System.Text.Encoding.UTF8.GetByteCount(str);
            return (int)Math.Ceiling(byteCount / 1024.0); // Use Math.Ceiling to round up
        }
        
        /// <summary>
        /// Gets the exact size of the string in bytes using UTF-8 encoding.
        /// </summary>
        public static int GetSizeInBytes(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return 0;

            return System.Text.Encoding.UTF8.GetByteCount(str);
        }

        public static DateTime? TryParseUtcNullable(this string? dateStr)
        {
            return DateTime.TryParse(dateStr, out var dt)
                ? dt.ToUniversalTime()
                : null;
        }
    }
}