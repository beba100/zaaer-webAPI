namespace zaaerIntegration.Services.Integrations
{
  /// <summary>
  /// Keeps integration log columns within DB limits (<c>error_message</c> is NVARCHAR(1000)).
  /// </summary>
  internal static class IntegrationResponseLogHelper
  {
    public const int MaxErrorMessageLength = 980;

    public static string? TruncateErrorMessage(string? message)
    {
      if (string.IsNullOrEmpty(message) || message.Length <= MaxErrorMessageLength)
      {
        return message;
      }

      return message[..MaxErrorMessageLength] + "…";
    }
  }
}
