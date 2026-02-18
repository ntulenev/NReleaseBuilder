namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Jira status reference value object that may hold one or many statuses.
/// </summary>
public readonly record struct JiraStatusReference
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraStatusReference"/> struct.
    /// </summary>
    /// <param name="value">Jira status reference text.</param>
    public JiraStatusReference(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalizedValue = value.Trim();
        var hasAtLeastOneStatus = normalizedValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length > 0;

        if (!hasAtLeastOneStatus)
        {
            throw new ArgumentException(
                "Jira status reference must contain at least one status value.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    /// <summary>
    /// Jira status reference text value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Splits the reference into individual Jira statuses.
    /// </summary>
    /// <returns>Distinct Jira statuses.</returns>
    public JiraStatusName[] SplitStatuses() =>
    [
        .. Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static status => new JiraStatusName(status))
            .Distinct()
    ];

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
