using System.Text.Json;
using System.Text.Json.Serialization;

namespace zaaerIntegration.Converters
{
    /// <summary>
    /// Custom JSON converter for nullable DateTime that preserves UTC timezone
    /// Prevents date shifting when converting UTC dates to local time
    /// </summary>
    public class NullableDateTimeUtcJsonConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Handle string values (including empty strings)
            if (reader.TokenType == JsonTokenType.String)
            {
                var stringValue = reader.GetString();
                // Treat empty strings, null, or whitespace as null
                if (string.IsNullOrWhiteSpace(stringValue) || stringValue == "\"\"" || stringValue == "null")
                {
                    return null;
                }
                
                // Try to parse as DateTime with UTC handling
                if (DateTime.TryParse(stringValue, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dateTimeValue))
                {
                    // If the string ends with 'Z' or has UTC indicator, ensure it's treated as UTC
                    if (stringValue.EndsWith("Z", StringComparison.OrdinalIgnoreCase) || 
                        stringValue.Contains("+00:00") || 
                        stringValue.Contains("UTC", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ensure the DateTime is in UTC
                        if (dateTimeValue.Kind == DateTimeKind.Unspecified)
                        {
                            dateTimeValue = DateTime.SpecifyKind(dateTimeValue, DateTimeKind.Utc);
                        }
                        else if (dateTimeValue.Kind == DateTimeKind.Local)
                        {
                            // Convert local to UTC
                            dateTimeValue = dateTimeValue.ToUniversalTime();
                        }
                        // If already UTC, keep it as is
                        return dateTimeValue;
                    }
                    
                    // For non-UTC dates, return as is
                    return dateTimeValue;
                }
                
                // If parsing fails but it's an empty-like value, return null
                return null;
            }

            // Handle null token
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            // For any other token type, try to deserialize using default behavior
            try
            {
                var result = JsonSerializer.Deserialize<DateTime?>(ref reader, options);
                // If the result is a UTC date, ensure it's marked as UTC
                if (result.HasValue && result.Value.Kind == DateTimeKind.Unspecified)
                {
                    // Check if it looks like a UTC date (common ISO 8601 format)
                    // For now, we'll assume dates from external systems are UTC if unspecified
                    result = DateTime.SpecifyKind(result.Value, DateTimeKind.Utc);
                }
                return result;
            }
            catch
            {
                // If deserialization fails, return null (graceful handling)
                return null;
            }
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                // Always write as UTC ISO 8601 format
                var utcValue = value.Value.Kind == DateTimeKind.Utc 
                    ? value.Value 
                    : value.Value.ToUniversalTime();
                writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }
}

