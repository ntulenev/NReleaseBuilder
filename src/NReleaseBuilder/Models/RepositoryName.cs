namespace NReleaseBuilder.Models;

/// <summary>
/// Repository identifier value object.
/// </summary>
public readonly struct RepositoryName : IEquatable<RepositoryName>, IComparable<RepositoryName>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryName"/> struct.
    /// </summary>
    /// <param name="value">Repository name value.</param>
    public RepositoryName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Repository name value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Attempts to create <see cref="RepositoryName"/> from string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <param name="repositoryName">Created repository name.</param>
    /// <returns><see langword="true"/> when value is valid.</returns>
    public static bool TryCreate(string? value, out RepositoryName repositoryName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            repositoryName = default;
            return false;
        }

        repositoryName = new RepositoryName(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(RepositoryName other)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is RepositoryName other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);
    }

    /// <inheritdoc />
    public int CompareTo(RepositoryName other)
    {
        return StringComparer.OrdinalIgnoreCase.Compare(Value, other.Value);
    }

    /// <summary>
    /// Checks equality between two <see cref="RepositoryName"/> values.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when values are equal.</returns>
    public static bool operator ==(RepositoryName left, RepositoryName right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Checks inequality between two <see cref="RepositoryName"/> values.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when values are not equal.</returns>
    public static bool operator !=(RepositoryName left, RepositoryName right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Compares whether left value is less than right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is less than right.</returns>
    public static bool operator <(RepositoryName left, RepositoryName right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Compares whether left value is less than or equal to right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is less than or equal to right.</returns>
    public static bool operator <=(RepositoryName left, RepositoryName right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Compares whether left value is greater than right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is greater than right.</returns>
    public static bool operator >(RepositoryName left, RepositoryName right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Compares whether left value is greater than or equal to right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is greater than or equal to right.</returns>
    public static bool operator >=(RepositoryName left, RepositoryName right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Value ?? string.Empty;
    }
}
