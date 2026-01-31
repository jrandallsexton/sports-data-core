using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Converters;

/// <summary>
/// Custom JSON converter for DateOnly that handles ESPN's DateTime format strings.
/// ESPN returns dates like "1975-05-09T07:00Z" but we only want the date portion.
/// </summary>
public class DateOnlyConverter : JsonConverter<DateOnly?>
{
    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // Try to parse as DateTime first (ESPN format: "1975-05-09T07:00Z")
            if (DateTime.TryParse(value, out var dateTime))
            {
                return DateOnly.FromDateTime(dateTime);
            }

            // Try to parse as DateOnly directly (format: "1975-05-09")
            if (DateOnly.TryParse(value, out var dateOnly))
            {
                return dateOnly;
            }

            throw new JsonException($"Unable to parse '{value}' as DateOnly.");
        }

        throw new JsonException($"Unexpected token type '{reader.TokenType}' when parsing DateOnly.");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
