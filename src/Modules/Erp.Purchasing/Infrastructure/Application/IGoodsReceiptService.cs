using Erp.Purchasing.Infrastructure.Contracts;

namespace Erp.Purchasing.Infrastructure.Application;

public interface IGoodsReceiptService
{
    Task<IReadOnlyList<GoodsReceiptListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default);

    Task<GoodsReceiptDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records goods arriving: writes the receipt, brings the stock in and credits the order lines
    /// it came against — all in one transaction.
    /// </summary>
    Task<GoodsReceiptDto> CreateAsync(
        CreateGoodsReceiptRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Undoes a receipt: takes the stock back out and gives the order back the quantity it had
    /// been credited with. Nothing is deleted — the receipt stays, voided, with its reason.
    /// </summary>
    Task<GoodsReceiptDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
