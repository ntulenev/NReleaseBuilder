namespace NReleaseBuilder.Helpers;

/// <summary>
/// Shared helpers for string normalization and formatting.
/// </summary>
internal static class StringHelpers
{
    /// <summary>
    /// Trims text to a single line and limits output length.
    /// </summary>
    /// <param name="value">Input text.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>Trimmed text, truncated with ellipsis when needed.</returns>
    public static string TrimText(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 0);

        var oneLine = value.ReplaceLineEndings(" ").Trim();
        if (oneLine.Length <= maxLength)
        {
            return oneLine;
        }

        return oneLine[..maxLength] + "...";
    }
}
