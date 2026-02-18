namespace NReleaseBuilder.Models.Bitbucket;

/// <summary>
/// Commit hash value object.
/// </summary>
public readonly record struct CommitHash
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommitHash"/> struct.
    /// </summary>
    /// <param name="value">Commit hash text.</param>
    public CommitHash(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Commit hash text value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates optional commit hash from raw value.
    /// </summary>
    /// <param name="value">Raw commit hash text.</param>
    /// <returns>Commit hash when value is present; otherwise <see langword="null"/>.</returns>
    public static CommitHash? FromOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new CommitHash(value);

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
