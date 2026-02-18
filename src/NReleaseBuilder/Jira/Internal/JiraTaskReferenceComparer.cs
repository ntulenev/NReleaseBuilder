

using NReleaseBuilder.Models.Jira;

namespace NReleaseBuilder.Jira.Internal;

/// <summary>
/// Case-insensitive comparer for Jira task references.
/// </summary>
internal sealed class JiraTaskReferenceComparer : IEqualityComparer<JiraTaskReference>
{
    /// <summary>
    /// Shared comparer instance.
    /// </summary>
    public static JiraTaskReferenceComparer Instance { get; } = new();

    /// <inheritdoc />
    public bool Equals(JiraTaskReference x, JiraTaskReference y)
        => StringComparer.OrdinalIgnoreCase.Equals(x.Value, y.Value);

    /// <inheritdoc />
    public int GetHashCode(JiraTaskReference obj)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Value ?? string.Empty);
}
