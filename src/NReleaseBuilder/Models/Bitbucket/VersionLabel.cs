namespace NReleaseBuilder.Models.Bitbucket;

/// <summary>
/// Version label value object.
/// </summary>
public readonly record struct VersionLabel
{
    private static readonly VersionLabel _notReleasedYetValue = new("Not released yet");

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

    /// <summary>
    /// Creates the sentinel label used for components that are not released yet.
    /// </summary>
    /// <returns>Not-released-yet version label.</returns>
    public static VersionLabel CreateNotReleasedYet() => _notReleasedYetValue;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}

