using System;
using System.Text.Json;

namespace SportsData.Core.Extensions;

public static class JsonExtensions
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
    };

    public static T? FromJson<T>(this string json, JsonSerializerOptions? options = null)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
    }

    public static object? FromJson(this string value, Type type, JsonSerializerOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return default;
        return JsonSerializer.Deserialize(value, type, options ?? DefaultOptions);
    }

    public static string ToJson(this object obj, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
    }
}
