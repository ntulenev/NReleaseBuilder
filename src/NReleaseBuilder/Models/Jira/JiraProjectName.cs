namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Jira project key value object.
/// </summary>
public readonly record struct JiraProjectName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraProjectName"/> struct.
    /// </summary>
    /// <param name="value">Jira project key.</param>
    public JiraProjectName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Jira project key value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
