namespace NReleaseBuilder.Models.Components;

/// <summary>
/// Row details message value object.
/// </summary>
public readonly record struct RowDetails
{
    private static readonly RowDetails _placeholderValue = new("-");

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

    /// <summary>
    /// Creates the placeholder details value used when no message is available.
    /// </summary>
    /// <returns>Placeholder row details.</returns>
    public static RowDetails CreatePlaceholder() => _placeholderValue;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

