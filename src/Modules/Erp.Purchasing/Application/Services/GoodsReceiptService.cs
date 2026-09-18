using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Common;
using Erp.FiscalPT;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// Goods arriving from suppliers. This is where purchases meet stock: a receipt brings the goods
/// into the warehouse and credits the order lines it came against.
/// </summary>
/// <remarks>
/// The receipt, the ledger entries and the order's received quantities are written in one
/// transaction. Any of them landing alone would leave the warehouse and the order disagreeing
/// about what arrived, and nobody would notice until a stock check.
/// </remarks>
public sealed class GoodsReceiptService(
    IGoodsReceiptStorage receiptStorage,
    IPurchaseOrderStorage orderStorage,
    IStockRecorder stockRecorder,
    IDocumentNumbers documentNumbers,
    IUnitOfWork unitOfWork) : IGoodsReceiptService
{
    /// <summary>Prefix of the receipt number. Ours, with no fiscal meaning.</summary>
    private const string NumberPrefix = "REC";

    /// <summary>The document type the stock ledger records these movements under.</summary>
    private const string DocumentType = "REC";

    public async Task<IReadOnlyList<GoodsReceiptListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        return [.. receipts.Select(MapListItem)];
    }

    public IQueryable<GoodsReceiptListItemDto> Query(Guid companyId) => receiptStorage.Query(companyId);

    public async Task<GoodsReceiptDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var receipt = await receiptStorage.GetByIdAsync(id, cancellationToken);
        return receipt is null ? null : Map(receipt);
    }

    public async Task<GoodsReceiptDto> CreateAsync(
        CreateGoodsReceiptRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);
        ArgumentNullException.ThrowIfNull(request.Lines);

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("A receipt has to say who the goods came from.", nameof(request));

        if (request.Lines.Count == 0)
            throw new ArgumentException("A receipt with no lines received nothing.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Inside the transaction, and not before it: the counter row is locked while the number is
        // taken, and a receipt that fails afterwards gives its number back instead of leaving a
        // hole in the sequence.
        var number = await documentNumbers.NextAsync(
            request.CompanyId, NumberPrefix, request.ReceiptDate.Year, cancellationToken);

        // Locked before anything is read from them: two receipts against the same order at the same
        // time would otherwise each credit a quantity the other had already taken.
        var orders = await ResolveOrdersAsync(request, cancellationToken);

        var receipt = GoodsReceipt.Create(
            request.CompanyId,
            request.SupplierId,
            ToSnapshot(request.Supplier),
            number,
            request.ReceiptDate,
            request.WarehouseId,
            [.. request.Lines.Select(line => ToLine(line, orders))],
            request.SupplierDocumentNumber,
            request.SupplierDocumentDate,
            request.Notes,
            userId);

        await receiptStorage.AddAsync(receipt, cancellationToken);

        foreach (var line in receipt.Lines.Where(line => line.OrderLineId is not null))
            orders[line.OrderId!.Value].RegisterReceipt(line.OrderLineId!.Value, line.Quantity);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await RecordStockAsync(receipt, userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(receipt);
    }

    public async Task<GoodsReceiptDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var receipt = await receiptStorage.GetByIdAsync(id, cancellationToken);
        if (receipt is null)
            return null;

        receipt.Void(reason, userId, DateTime.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The order gets its quantity back, so what the supplier still owes is right again.
        foreach (var line in receipt.Lines.Where(line => line.OrderId is not null))
        {
            var order = await orderStorage.GetForUpdateAsync(line.OrderId!.Value, cancellationToken);
            order?.ReverseReceipt(line.OrderLineId!.Value, line.Quantity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await stockRecorder.ReverseDocumentAsync(
            receipt.Id, $"Anulação de {receipt.Number}: {reason}", userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(receipt);
    }

    /// <summary>
    /// Loads and locks every order the receipt touches, and refuses to receive more than is still
    /// owed. Reading the pending quantity without the lock is what would let two receipts both
    /// pass �?" the same problem, and the same answer, as invoicing from delivery notes in Sales.
    /// </summary>
    private async Task<Dictionary<Guid, PurchaseOrder>> ResolveOrdersAsync(
        CreateGoodsReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var orders = new Dictionary<Guid, PurchaseOrder>();
        var claimed = new Dictionary<Guid, decimal>();

        foreach (var line in request.Lines)
        {
            if (line.OrderLineId is not { } orderLineId)
                continue;

            var order = orders.Values.FirstOrDefault(x => x.Lines.Any(y => y.Id == orderLineId));

            if (order is null)
            {
                order = await orderStorage.GetForUpdateByLineAsync(orderLineId, cancellationToken)
                    ?? throw new ArgumentException(
                        $"Order line '{orderLineId}' was not found.", nameof(request));

                if (order.CompanyId != request.CompanyId)
                    throw new ArgumentException("The order does not belong to the requested company.", nameof(request));

                if (order.SupplierId != request.SupplierId)
                    throw new ArgumentException("The order belongs to another supplier.", nameof(request));

                orders[order.Id] = order;
            }

            var orderLine = order.Lines.First(x => x.Id == orderLineId);

            // Counted across the request too: the same order line twice in one receipt has to add
            // up against the same pending quantity.
            claimed.TryGetValue(orderLineId, out var already);
            claimed[orderLineId] = already + line.Quantity;

            if (claimed[orderLineId] > orderLine.PendingQuantity)
            {
                throw new InvalidOperationException(
                    $"Line '{orderLine.ProductCode}' of order '{order.Number}' has only " +
                    $"{orderLine.PendingQuantity:0.###} left to receive.");
            }
        }

        return orders;
    }

    /// <summary>
    /// Brings the goods into the warehouse. The unit cost travels with the entry: purchases are the
    /// only place a real cost enters the ledger.
    /// </summary>
    private async Task RecordStockAsync(GoodsReceipt receipt, string? userId, CancellationToken cancellationToken)
    {
        var request = new RecordDocumentStockRequest(
            receipt.CompanyId,
            receipt.WarehouseId,
            StockDirection.In,
            receipt.ReceiptDate,
            DocumentType,
            receipt.Number,
            receipt.Id,
            [.. receipt.Lines.Select(line => new DocumentStockLine(
                line.Id,
                line.ProductCode,
                line.ProductDescription,
                line.Quantity,
                // The stock ledger values goods at what they actually cost, discount included.
                UnitCost: line.Quantity == 0 ? 0m : FiscalRounding.Amount(line.LineAmount / line.Quantity)))]);

        await stockRecorder.RecordAsync(request, userId, cancellationToken);
    }

    private static GoodsReceiptLine ToLine(
        GoodsReceiptLineRequest request,
        IReadOnlyDictionary<Guid, PurchaseOrder> orders)
    {
        var order = request.OrderLineId is { } orderLineId
            ? orders.Values.FirstOrDefault(x => x.Lines.Any(y => y.Id == orderLineId))
            : null;

        var orderLine = order?.Lines.FirstOrDefault(x => x.Id == request.OrderLineId);

        // The order line is the source document here: its discount is what the receipt inherits
        // unless the caller overrides it.
        var discountPercentage = request.DiscountPercentage != 0m
            ? request.DiscountPercentage
            : orderLine?.DiscountPercentage ?? 0m;

        var grossAmount = FiscalRounding.Amount(request.Quantity * request.UnitCost);
        var discountAmount = FiscalRounding.Amount(grossAmount * discountPercentage / 100m);

        return new GoodsReceiptLine
        {
            OrderId = order?.Id,
            OrderLineId = request.OrderLineId,
            ProductCode = request.ProductCode.Trim(),
            ProductDescription = request.ProductDescription.Trim(),
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitCost = request.UnitCost,
            DiscountPercentage = discountPercentage,
            DiscountAmount = discountAmount,
            LineAmount = grossAmount - discountAmount
        };
    }

    private static SupplierSnapshot ToSnapshot(PurchaseOrderSupplierDto supplier) =>
        new(supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country);

    private static GoodsReceiptListItemDto MapListItem(GoodsReceipt receipt) =>
        new()
        {
            Id = receipt.Id,
            Number = receipt.Number,
            Status = receipt.Status.ToString(),
            ReceiptDate = receipt.ReceiptDate,
            SupplierName = receipt.Supplier.Name,
            SupplierDocumentNumber = receipt.SupplierDocumentNumber,
            LineCount = receipt.Lines.Count,
            TotalCost = receipt.TotalCost,
            IsVoided = receipt.IsVoided
        };

    private static GoodsReceiptDto Map(GoodsReceipt receipt) =>
        new(receipt.Id,
            receipt.CompanyId,
            receipt.SupplierId,
            receipt.Number,
            receipt.Status.ToString(),
            receipt.ReceiptDate,
            receipt.WarehouseId,
            receipt.SupplierDocumentNumber,
            receipt.SupplierDocumentDate,
            new PurchaseOrderSupplierDto(
                receipt.Supplier.Code,
                receipt.Supplier.Name,
                receipt.Supplier.TaxId,
                receipt.Supplier.Address,
                receipt.Supplier.PostalCode,
                receipt.Supplier.City,
                receipt.Supplier.Country),
            receipt.Notes,
            receipt.TotalCost,
            receipt.CreatedAtUtc,
            receipt.VoidedAtUtc,
            receipt.VoidReason,
            [.. receipt.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new GoodsReceiptLineDto(
                    line.Id,
                    line.LineNumber,
                    line.OrderId,
                    line.OrderLineId,
                    line.ProductCode,
                    line.ProductDescription,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitCost,
                    line.DiscountPercentage,
                    line.DiscountAmount,
                    line.LineAmount))]);
}
