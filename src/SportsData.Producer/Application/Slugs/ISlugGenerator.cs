using System.Text.RegularExpressions;

namespace SportsData.Producer.Application.Slugs
{
    public static class SlugGenerator
    {
        public static string GenerateSlug(params string[] inputs)
        {
            var input = inputs.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var slug = input.ToLowerInvariant()
                .Replace("&", "and")
                .Replace("(", "")
                .Replace(")", "")
                .Replace(".", "")
                .Replace(",", "")
                .Replace("/", "-")
                .Replace("'", "")
                .Replace(" ", "-");

            slug = Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug = Regex.Replace(slug, @"\-{2,}", "-");

            return slug.Trim('-');
        }
    }
}