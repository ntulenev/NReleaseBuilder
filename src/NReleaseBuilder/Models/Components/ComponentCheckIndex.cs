namespace NReleaseBuilder.Models.Components;

/// <summary>
/// Display index value object for component check rows.
/// </summary>
public readonly record struct ComponentCheckIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentCheckIndex"/> struct.
    /// </summary>
    /// <param name="value">Display index value.</param>
    public ComponentCheckIndex(int value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);

        Value = value;
    }

    /// <summary>
    /// Display index value.
    /// </summary>
    public int Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

