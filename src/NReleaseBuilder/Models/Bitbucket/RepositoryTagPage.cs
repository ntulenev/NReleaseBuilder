namespace NReleaseBuilder.Models.Bitbucket;

/// <summary>
/// Repository tag page domain model.
/// </summary>
public sealed class RepositoryTagPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryTagPage"/> class.
    /// </summary>
    /// <param name="values">Page tag references.</param>
    /// <param name="next">Next page URL.</param>
    public RepositoryTagPage(IReadOnlyList<RepositoryTagReference> values, Uri? next)
    {
        ArgumentNullException.ThrowIfNull(values);

        Values = values;
        Next = next;
    }

    /// <summary>
    /// Empty tag page.
    /// </summary>
    public static RepositoryTagPage Empty { get; } = new([], null);

    /// <summary>
    /// Page tag references.
    /// </summary>
    public IReadOnlyList<RepositoryTagReference> Values { get; }

    /// <summary>
    /// Next page URL.
    /// </summary>
    public Uri? Next { get; }
}
