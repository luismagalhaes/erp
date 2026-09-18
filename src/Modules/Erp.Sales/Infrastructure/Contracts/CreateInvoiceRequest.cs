namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="RectifiedDocumentId">
/// The document being corrected. Required for a credit or debit note, refused on any other type.
/// </param>
/// <param name="RectificationReason">Why the document is being corrected.</param>
/// <param name="DueDate">Until when the customer has to pay. Never before the document date.</param>
/// <param name="Payments">
/// How a fatura-recibo was paid, adding up to its total. Required for FR, refused on every other type.
/// </param>
public sealed record CreateInvoiceRequest(
    Guid CompanyId,
    Guid SeriesId,
    DateOnly DocumentDate,
    CustomerRequest Customer,
    IReadOnlyList<CreateInvoiceLineRequest> Lines,
    Guid? RectifiedDocumentId = null,
    string? RectificationReason = null,
    Guid? WarehouseId = null,
    DateOnly? DueDate = null,
    IReadOnlyList<CreatePaymentMethodRequest>? Payments = null);
