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
        string NormalizeAndHash(string json);
    }

    public class JsonHashCalculator : IJsonHashCalculator
    {
        public string NormalizeAndHash(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var normalized = Normalize(doc.RootElement);
            var serialized = JsonSerializer.Serialize(normalized, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(serialized);
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }

        private static object? Normalize(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject()
                    .OrderBy(p => p.Name)
                    .ToDictionary(p => p.Name, p => Normalize(p.Value)),
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

}