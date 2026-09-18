namespace Erp.Main.Models.Sales;

/// <param name="Payments">Required for a fatura-recibo, and only for it.</param>
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
