namespace Erp.Purchasing.Infrastructure.Contracts;

/// <summary>
/// A line the supplier still owes. Knowing what is outstanding is the whole point of the order
/// existing, so it is a query of its own rather than a filter over the list.
/// </summary>
public sealed record PendingOrderLineDto(
    Guid OrderId,
    string OrderNumber,
    DateOnly OrderDate,
    DateOnly? ExpectedDate,
    Guid SupplierId,
    string SupplierName,
    Guid WarehouseId,
    Guid LineId,
    string ProductCode,
    string ProductDescription,
    string UnitOfMeasure,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal PendingQuantity,
    decimal UnitPrice);
