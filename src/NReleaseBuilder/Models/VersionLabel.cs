namespace NReleaseBuilder.Models;

/// <summary>
/// Version label value object.
/// </summary>
public readonly record struct VersionLabel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionLabel"/> struct.
    /// </summary>
    /// <param name="value">Version text.</param>
    public VersionLabel(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Version text value.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
