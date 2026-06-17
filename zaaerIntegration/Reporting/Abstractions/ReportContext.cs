namespace zaaerIntegration.Reporting.Abstractions;

public sealed class ReportContext
{
    public required string ReportKey { get; init; }
    public required string ReportVersion { get; init; }
    public string? HotelCode { get; init; }
    public int? UserId { get; init; }
    public string Culture { get; init; } = "ar-SA";
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }

    public int GetRequiredInt(string name)
    {
        if (!Parameters.TryGetValue(name, out var value) || value is null)
        {
            throw new ArgumentException($"Report parameter '{name}' is required.", nameof(Parameters));
        }

        return value switch
        {
            int i => i,
            long l => checked((int)l),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => Convert.ToInt32(value)
        };
    }
}
