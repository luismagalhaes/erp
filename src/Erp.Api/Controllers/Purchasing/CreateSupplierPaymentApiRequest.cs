using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// What a client sends to record a payment to a supplier. It names the supplier; the host reads the
/// supplier file from Core and copies it onto the record.
/// </summary>
public sealed record CreateSupplierPaymentApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly PaymentDate,
    IReadOnlyList<SupplierPaymentLineRequest> Lines,
    IReadOnlyList<SupplierPaymentMethodRequest> Methods,
    string? Description = null);
