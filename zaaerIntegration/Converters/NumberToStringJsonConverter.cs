using System.Text.Json;
using System.Text.Json.Serialization;

namespace zaaerIntegration.Converters
{
    /// <summary>
    /// Custom JSON converter that accepts both numbers and strings, converting them to string
    /// Used for fields like ApartmentCode and ApartmentName that may come as numbers from external systems
    /// </summary>
    public class NumberToStringJsonConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle number token - convert to string
            if (reader.TokenType == JsonTokenType.Number)
            {
                // Try to read as Int64 first (handles large numbers)
                if (reader.TryGetInt64(out var intValue))
                {
                    return intValue.ToString();
                }
                // If not an integer, try as double
                if (reader.TryGetDouble(out var doubleValue))
                {
                    // Remove decimal point if it's a whole number
                    if (doubleValue % 1 == 0)
                    {
                        return ((long)doubleValue).ToString();
                    }
                    return doubleValue.ToString();
                }
            }

            // Handle string token - return as is
            if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString() ?? string.Empty;
            }

            // Handle null token
            if (reader.TokenType == JsonTokenType.Null)
            {
                return string.Empty;
            }

            // For any other token type, convert to string
            return reader.GetString() ?? string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            // Always write as string
            writer.WriteStringValue(value ?? string.Empty);
        }
    }
}

