namespace Erp.FiscalPT.Saft;

/// <summary>
/// What a source is being asked for: the company's own documents for a period, and — on a
/// self-billing file — whose documents.
/// </summary>
/// <param name="SubjectTaxId">
/// The entity the file is about, when it is not the company itself. Null on a billing file, where
/// the subject is the company. On a self-billing file it is the supplier: the invoices are their
/// sales, issued by us in their name, so there is one file per supplier.
/// </param>
/// <param name="SelfBiller">
/// Who issued on the subject's behalf — us. Null on a billing file, where nobody self-bills. On a
/// self-billing file this is what goes in the <c>Customer</c> table, because the document titles a
/// sale <b>of the supplier's</b> and in that sale the customer is us.
/// </param>
public sealed record SaftSourceRequest(
    Guid CompanyId,
    DateOnly StartDate,
    DateOnly EndDate,
    string FileType,
    string? SubjectTaxId = null,
    SaftEntityInfo? SelfBiller = null);
