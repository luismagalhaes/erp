using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<PurchaseOrderListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        bool openOnly = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The listing as a query, so filtering, sorting and paging can be applied by the database on
    /// behalf of the data grid.
    /// </summary>
    IQueryable<PurchaseOrderListItemDto> Query(Guid companyId);

    Task<PurchaseOrderDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What suppliers still owe, line by line. Feeds the goods receipt in phase 2.</summary>
    Task<IReadOnlyList<PendingOrderLineDto>> GetPendingLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<PurchaseOrderDto> CreateAsync(
        CreatePurchaseOrderRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Rewrites an order that has not started receiving goods.</summary>
    Task<PurchaseOrderDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a draft to the supplier.</summary>
    Task<PurchaseOrderDto?> PlaceAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Closes an order that will not be completed, writing off what is still owed.</summary>
    Task<PurchaseOrderDto?> CloseAsync(
        Guid id,
        ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Calls off an order. Only possible while nothing has arrived.</summary>
    Task<PurchaseOrderDto?> CancelAsync(
        Guid id,
        ClosePurchaseOrderRequest request,
        CancellationToken cancellationToken = default);
}
