namespace Erp.Purchasing.Infrastructure.Contracts;

public sealed record SupplierReturnListItemDto(
    Guid Id,
    string Number,
    string Status,
    DateOnly ReturnDate,
    string SupplierName,
    string Reason,
    int LineCount,
    decimal TotalCost,
    bool IsVoided);
