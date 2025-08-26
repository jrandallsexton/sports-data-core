using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsData.Core.Converters;

public class FlexibleStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            _ => throw new JsonException($"Unexpected token parsing string. TokenType: {reader.TokenType}")
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}