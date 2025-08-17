using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsData.Core.Common.Hashing
{
    public interface IJsonHashCalculator
    {
        string NormalizeAndHash(string? json);
    }

    public class JsonHashCalculator : IJsonHashCalculator
    {
        // SHA256 of empty string: e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
        private static readonly string EmptyHash = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>()));

        public string NormalizeAndHash(string? json)
        {
            if (json is null)
                return EmptyHash;

            // Trim whitespace and handle optional UTF-8 BOM
            json = json.Trim();
            if (json.Length > 0 && json[0] == '\uFEFF') // BOM
                json = json[1..].Trim();

            if (json.Length == 0)
                return EmptyHash;

            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var normalized = Normalize(doc.RootElement);
            var serialized = JsonSerializer.Serialize(
                normalized,
                new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

            var bytes = Encoding.UTF8.GetBytes(serialized);
            return Convert.ToHexString(SHA256.HashData(bytes));
        }

        private static object? Normalize(JsonElement element) =>
            element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal) // make ordering explicit
                    .ToDictionary(p => p.Name, p => Normalize(p.Value), StringComparer.Ordinal),
                JsonValueKind.Array => element.EnumerateArray().Select(Normalize).ToList(),
                JsonValueKind.String => (string?)element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => throw new NotSupportedException($"Unsupported JSON value kind: {element.ValueKind}")
            };
    }
}
