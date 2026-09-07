using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

public interface ISupplierReturnService
{
    Task<IReadOnlyList<SupplierReturnListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<SupplierReturnDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>What is still in hand from each receipt, and so could go back.</summary>
    Task<IReadOnlyList<ReturnableReceiptLineDto>> GetReturnableLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends goods back: takes the stock out of the warehouse, at the cost it came in at.
    /// </summary>
    /// <remarks>
    /// This records the movement. Goods that physically travel also need a transport document, and
    /// that one <b>is</b> fiscal and belongs to Sales — a delivery note of type <c>GD</c>.
    /// </remarks>
    Task<SupplierReturnDto> CreateAsync(
        CreateSupplierReturnRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Undoes a return: the stock comes back in and the goods are ours again.</summary>
    Task<SupplierReturnDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
