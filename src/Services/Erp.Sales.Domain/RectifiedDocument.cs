namespace Erp.Sales.Domain;

/// <summary>
/// The document a credit or debit note corrects, as it stood when the note was issued. Article
/// 36.º n.º 5 of the CIVA requires a rectifying document to identify the one it rectifies, and the
/// SAF-T carries it per line under References.
/// </summary>
/// <param name="DocumentNumber">
/// Copied rather than looked up, like every other snapshot: the printed note and the SAF-T must
/// keep reading correctly whatever happens later.
/// </param>
public sealed record RectifiedDocument(Guid DocumentId, string DocumentNumber, string Reason);
