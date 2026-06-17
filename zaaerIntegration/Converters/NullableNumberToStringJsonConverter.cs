using System.Text.Json;
using System.Text.Json.Serialization;

namespace zaaerIntegration.Converters
{
    /// <summary>
    /// Custom JSON converter that accepts both numbers and strings for nullable string properties, converting them to string
    /// Used for nullable fields like ApartmentName that may come as numbers from external systems
    /// </summary>
    public class NullableNumberToStringJsonConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
                return reader.GetString();
            }

            // Handle null token - return null for nullable properties
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // For any other token type, try to get string or return null
            return reader.GetString();
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            // Write as string or null
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value);
            }
        }
    }
}

