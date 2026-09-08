using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// The wire form of a self-billed invoice. The supplier snapshot is not part of it: the host reads
/// the supplier from Core and copies it onto the document, so the caller cannot invent one.
/// </summary>
public sealed record IssueSelfBilledInvoiceApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    Guid SeriesId,
    DateOnly IssueDate,
    IReadOnlyList<SelfBilledInvoiceLineRequest> Lines,
    string? SupplierAgreementReference = null);
