using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SportsData.Core.Converters
{
    public class ParseStringToIntConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String &&
                int.TryParse(reader.GetString(), out var value))
            {
                return value;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32();
            }

            throw new JsonException("Expected string or number for int value.");
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}