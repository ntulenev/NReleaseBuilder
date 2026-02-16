namespace NReleaseBuilder.Models;

/// <summary>
/// Error message value object.
/// </summary>
public readonly record struct ErrorMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorMessage"/> struct.
    /// </summary>
    /// <param name="value">Error text.</param>
    public ErrorMessage(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Error text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
