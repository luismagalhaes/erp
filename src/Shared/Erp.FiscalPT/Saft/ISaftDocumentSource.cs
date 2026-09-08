namespace Erp.FiscalPT.Saft;

/// <summary>
/// A module that has documents to put in a SAF-T file. Sales contributes to the billing file;
/// self-billing will contribute to its own.
/// </summary>
/// <remarks>
/// The point of asking rather than reading is that the SAF-T is a file of the **company**, not of
/// the sales module — it always needed the company from Core, and now it needs documents from more
/// than one place. A source says which file type it serves, so a self-billing document can never
/// end up in the billing file by accident.
/// </remarks>
public interface ISaftDocumentSource
{
    /// <summary>The file type this source has documents for; see <see cref="SaftFileType"/>.</summary>
    string FileType { get; }

    Task<SaftSourceContent> GetContentAsync(
        SaftSourceRequest request,
        CancellationToken cancellationToken = default);
}
