using QuestPDF.Infrastructure;

namespace NReleaseBuilder.Abstractions.Rendering;

/// <summary>
/// Abstraction for persisting generated PDF report content.
/// </summary>
public interface IPdfReportFileStore
{
    /// <summary>
    /// Generates and saves a PDF document to the specified output path.
    /// </summary>
    /// <param name="outputPath">Absolute or relative output path.</param>
    /// <param name="document">Document to generate and persist.</param>
    void Save(string outputPath, IDocument document);
}
