namespace NReleaseBuilder.Models;

/// <summary>
/// Jira title reference value object that may hold one or many titles.
/// </summary>
public readonly record struct JiraTitleReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTitleReference"/> struct.
    /// </summary>
    /// <param name="value">Jira title reference text.</param>
    public JiraTitleReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalizedValue = value.Trim();
        var hasAtLeastOneTitle = normalizedValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length > 0;

        if (!hasAtLeastOneTitle)
        {
            throw new ArgumentException(
                "Jira title reference must contain at least one title value.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    /// <summary>
    /// Jira title reference text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
