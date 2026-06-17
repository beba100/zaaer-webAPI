using System.Text.Json;
using System.Text.Json.Serialization;

namespace zaaerIntegration.Converters
{
    /// <summary>
    /// JSON converter that ensures decimal values are serialized with exactly 2 decimal places
    /// Used for financial amounts in VoM journal entries to avoid precision issues
    /// </summary>
    public class DecimalTwoPlacesJsonConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetDecimal(out var dec))
                {
                    // Round to 2 decimal places when reading
                    return Math.Round(dec, 2, MidpointRounding.AwayFromZero);
                }
                return 0m;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s) || s == "\"\"" || s == "null")
                    return 0m;
                if (decimal.TryParse(s, out var dec))
                {
                    // Round to 2 decimal places when reading
                    return Math.Round(dec, 2, MidpointRounding.AwayFromZero);
                }
                return 0m;
            }
            return 0m;
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            // Round to 2 decimal places and write as number
            var roundedValue = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            writer.WriteNumberValue(roundedValue);
        }
    }
}

