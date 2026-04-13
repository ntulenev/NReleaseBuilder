namespace NReleaseBuilder.Models.Jira;

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

        var normalizedValue = value.Trim();
        var hasAtLeastOneTask = normalizedValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length > 0;

        if (!hasAtLeastOneTask)
        {
            throw new ArgumentException(
                "Jira task reference must contain at least one task value.",
                nameof(value));
        }

        Value = normalizedValue;
    }

    /// <summary>
    /// Jira task reference text value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets a not-available Jira task reference.
    /// </summary>
    public static JiraTaskReference NotAvailable { get; } = new("N/A");

    /// <summary>
    /// Attempts to create a Jira task reference from string.
    /// </summary>
    /// <param name="value">Raw task reference value.</param>
    /// <param name="jiraTaskReference">Created Jira task reference.</param>
    /// <returns><see langword="true"/> when value is valid.</returns>
    public static bool TryCreate(string? value, out JiraTaskReference jiraTaskReference)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            jiraTaskReference = default;
            return false;
        }

        try
        {
            jiraTaskReference = new JiraTaskReference(value);
            return true;
        }
        catch (ArgumentException)
        {
            jiraTaskReference = default;
            return false;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
