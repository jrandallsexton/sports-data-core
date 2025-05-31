using System;
using System.Text.RegularExpressions;

namespace SportsData.Core.Common.Routing
{
    public interface IGenerateRoutingKeys
    {
        string Generate(SourceDataProvider provider, string url);
    }

    public class RoutingKeyGenerator : IGenerateRoutingKeys
    {
        public string Generate(SourceDataProvider provider, string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL must not be null or empty", nameof(url));

            var uri = new Uri(url);
            var path = uri.AbsolutePath;

            // Remove leading "/v2/" or similar fixed API versioning
            path = Regex.Replace(path, @"^/v\d+/", "/");

            // Normalize to dot-separated routing key
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var key = string.Join('.', segments);

            return string.IsNullOrEmpty(key) ? key : $"{provider.ToString().ToLower()}.{key}";
        }
    }
}