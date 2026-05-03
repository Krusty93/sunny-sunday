namespace SunnySunday.Cli.Infrastructure;

/// <summary>
/// Validates the SUNNY_SERVER environment variable value.
/// </summary>
public static class ServerUrlValidator
{
    public enum ValidationResult
    {
        Valid,
        Missing,
        Malformed,
    }

    /// <summary>
    /// Validates <paramref name="value"/> and, on success, outputs the parsed <see cref="Uri"/>.
    /// </summary>
    public static ValidationResult Validate(string? value, out Uri? uri)
    {
        uri = null;

        if (string.IsNullOrWhiteSpace(value))
            return ValidationResult.Missing;

        if (!Uri.TryCreate(value, UriKind.Absolute, out uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            uri = null;
            return ValidationResult.Malformed;
        }

        return ValidationResult.Valid;
    }
}
