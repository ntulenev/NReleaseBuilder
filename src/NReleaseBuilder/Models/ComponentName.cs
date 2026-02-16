namespace NReleaseBuilder.Models;

/// <summary>
/// Component name value object.
/// </summary>
public readonly record struct ComponentName
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ComponentName"/> struct.
    /// </summary>
    /// <param name="value">Component name text.</param>
    public ComponentName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Component name text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
