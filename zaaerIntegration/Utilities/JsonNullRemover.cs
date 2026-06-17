using System.Text.Json;

namespace zaaerIntegration.Utilities
{
    /// <summary>
    /// Utility class to remove null values from JSON strings
    /// Used to match VoM API requirements which don't accept null keys
    /// </summary>
    public static class JsonNullRemover
    {
        /// <summary>
        /// Remove null values from JSON string to match VoM API requirements
        /// </summary>
        public static string RemoveNullValues(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return json;

            try
            {
                using var doc = JsonDocument.Parse(json);
                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
                {
                    WriteJsonElementWithoutNulls(writer, doc.RootElement);
                }
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
            catch
            {
                // If parsing fails, return original JSON
                return json;
            }
        }

        /// <summary>
        /// Recursively write JSON element excluding null values
        /// </summary>
        private static void WriteJsonElementWithoutNulls(Utf8JsonWriter writer, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in element.EnumerateObject())
                    {
                        // Skip null values
                        if (property.Value.ValueKind == JsonValueKind.Null)
                            continue;
                        
                        writer.WritePropertyName(property.Name);
                        WriteJsonElementWithoutNulls(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in element.EnumerateArray())
                    {
                        WriteJsonElementWithoutNulls(writer, item);
                    }
                    writer.WriteEndArray();
                    break;
                case JsonValueKind.String:
                    writer.WriteStringValue(element.GetString());
                    break;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var intValue))
                        writer.WriteNumberValue(intValue);
                    else if (element.TryGetInt64(out var longValue))
                        writer.WriteNumberValue(longValue);
                    else
                        writer.WriteNumberValue(element.GetDecimal());
                    break;
                case JsonValueKind.True:
                    writer.WriteBooleanValue(true);
                    break;
                case JsonValueKind.False:
                    writer.WriteBooleanValue(false);
                    break;
                case JsonValueKind.Null:
                    // Skip null values
                    break;
                default:
                    writer.WriteStringValue(element.GetRawText());
                    break;
            }
        }
    }
}
