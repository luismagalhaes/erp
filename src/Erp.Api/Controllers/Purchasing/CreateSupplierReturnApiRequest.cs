using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Api.Controllers.Purchasing;

/// <summary>
/// What a client sends to return goods. It names the supplier; the host reads the supplier file
/// from Core and copies it onto the return. The warehouse is not asked for — the goods leave the
/// one they are sitting in, which the receipt already says.
/// </summary>
public sealed record CreateSupplierReturnApiRequest(
    Guid CompanyId,
    Guid SupplierId,
    DateOnly ReturnDate,
    string Reason,
    IReadOnlyList<SupplierReturnLineRequest> Lines,
    string? Notes = null);
