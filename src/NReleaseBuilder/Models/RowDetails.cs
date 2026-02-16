namespace NReleaseBuilder.Models;

/// <summary>
/// Row details message value object.
/// </summary>
public readonly record struct RowDetails
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RowDetails"/> struct.
    /// </summary>
    /// <param name="value">Details message text.</param>
    public RowDetails(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Details message text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
