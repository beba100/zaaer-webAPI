using System.Text;
using System.Text.Json;
using Zatca.EInvoice.Exceptions;

namespace zaaerIntegration.Services.Integrations.Zatca
{
  internal static class ZatcaComplianceApiErrorParser
  {
    public static string Format(ZatcaApiException ex)
    {
      var sb = new StringBuilder(ex.Message);
      if (string.IsNullOrWhiteSpace(ex.Response))
      {
        return sb.ToString();
      }

      try
      {
        using var doc = JsonDocument.Parse(ex.Response);
        var root = doc.RootElement;
        if (root.TryGetProperty("validationResults", out var vr))
        {
          AppendMessages(sb, vr, "errorMessages");
          AppendMessages(sb, vr, "warningMessages");
          if (vr.TryGetProperty("status", out var status))
          {
            sb.Append($" [status: {status.GetString()}]");
          }
        }
        else if (root.TryGetProperty("message", out var msg))
        {
          sb.Append(" — ").Append(msg.GetString());
        }
        else
        {
          sb.Append(" — ").Append(Truncate(ex.Response, 600));
        }
      }
      catch
      {
        sb.Append(" — ").Append(Truncate(ex.Response, 600));
      }

      return sb.ToString();
    }

    private static void AppendMessages(StringBuilder sb, JsonElement validationResults, string arrayName)
    {
      if (!validationResults.TryGetProperty(arrayName, out var arr) || arr.ValueKind != JsonValueKind.Array)
      {
        return;
      }

      foreach (var item in arr.EnumerateArray())
      {
        var code = item.TryGetProperty("code", out var c) ? c.GetString() : null;
        var message = item.TryGetProperty("message", out var m) ? m.GetString() : null;
        if (string.IsNullOrWhiteSpace(message))
        {
          continue;
        }

        sb.Append(" | ");
        if (!string.IsNullOrWhiteSpace(code))
        {
          sb.Append('[').Append(code).Append("] ");
        }

        sb.Append(message);
      }
    }

    private static string Truncate(string value, int max) =>
      value.Length <= max ? value : value[..max] + "…";
  }
}
