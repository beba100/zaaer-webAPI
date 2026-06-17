using System.Text.Json;
using System.Text.Json.Serialization;

namespace zaaerIntegration.Converters
{
    /// <summary>
    /// Custom JSON converter that accepts both numbers and strings, converting them to int
    /// Used for fields like FloorNumber that may come as strings from external systems
    /// </summary>
    public class StringToIntJsonConverter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle number token - return as is
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32();
            }

            // Handle string token - try to parse as int
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                if (string.IsNullOrWhiteSpace(stringValue))
                {
                    throw new JsonException("Cannot convert empty string to int.");
                }
                
                if (int.TryParse(stringValue, out var intValue))
                {
                    return intValue;
                }
                
                throw new JsonException($"Cannot convert '{stringValue}' to int.");
            }

            // Handle null token
            if (reader.TokenType == JsonTokenType.Null)
            {
                throw new JsonException("Cannot convert null to int.");
            }

            // For any other token type, try to get int
            throw new JsonException($"Unexpected token type {reader.TokenType} when converting to int.");
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        {
            // Always write as number
            writer.WriteNumberValue(value);
        }
    }
}

