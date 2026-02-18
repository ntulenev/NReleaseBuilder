namespace NReleaseBuilder.Models.Jira;

/// <summary>
/// Jira status value object.
/// </summary>
public readonly struct JiraStatusName : IEquatable<JiraStatusName>, IComparable<JiraStatusName>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraStatusName"/> struct.
    /// </summary>
    /// <param name="value">Status text.</param>
    public JiraStatusName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value.Trim();
    }

    /// <summary>
    /// Jira status text value.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Attempts to create <see cref="JiraStatusName"/> from string.
    /// </summary>
    /// <param name="value">Input value.</param>
    /// <param name="statusName">Created status value.</param>
    /// <returns><see langword="true"/> when value is valid.</returns>
    public static bool TryCreate(string? value, out JiraStatusName statusName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            statusName = default;
            return false;
        }

        statusName = new JiraStatusName(value);
        return true;
    }

    /// <inheritdoc />
    public bool Equals(JiraStatusName other) => StringComparer.OrdinalIgnoreCase.Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is JiraStatusName other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value ?? string.Empty);

    /// <inheritdoc />
    public int CompareTo(JiraStatusName other) => StringComparer.OrdinalIgnoreCase.Compare(Value, other.Value);

    /// <summary>
    /// Checks equality between two <see cref="JiraStatusName"/> values.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when values are equal.</returns>
    public static bool operator ==(JiraStatusName left, JiraStatusName right) => left.Equals(right);

    /// <summary>
    /// Checks inequality between two <see cref="JiraStatusName"/> values.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when values are not equal.</returns>
    public static bool operator !=(JiraStatusName left, JiraStatusName right) => !left.Equals(right);

    /// <summary>
    /// Compares whether left value is less than right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is less than right.</returns>
    public static bool operator <(JiraStatusName left, JiraStatusName right) => left.CompareTo(right) < 0;

    /// <summary>
    /// Compares whether left value is less than or equal to right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is less than or equal to right.</returns>
    public static bool operator <=(JiraStatusName left, JiraStatusName right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// Compares whether left value is greater than right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is greater than right.</returns>
    public static bool operator >(JiraStatusName left, JiraStatusName right) => left.CompareTo(right) > 0;

    /// <summary>
    /// Compares whether left value is greater than or equal to right value.
    /// </summary>
    /// <param name="left">Left value.</param>
    /// <param name="right">Right value.</param>
    /// <returns><see langword="true"/> when left is greater than or equal to right.</returns>
    public static bool operator >=(JiraStatusName left, JiraStatusName right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => Value ?? string.Empty;
}
