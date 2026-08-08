namespace SportsData.Api.Application.Admin.Prompts;

/// <summary>
/// Strips control characters (CR/LF included) from operator-supplied
/// strings before they reach log templates — prevents log-entry forging
/// (CodeQL: log entries created from user input).
/// </summary>
public static class LogSanitizer
{
    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return new string(value.Where(c => !char.IsControl(c)).ToArray());
    }
}
