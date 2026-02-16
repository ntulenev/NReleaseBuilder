namespace NReleaseBuilder.Models;

/// <summary>
/// Jira task reference value object.
/// </summary>
public readonly record struct JiraTaskReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraTaskReference"/> struct.
    /// </summary>
    /// <param name="value">Jira task reference text.</param>
    public JiraTaskReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Jira task reference text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
