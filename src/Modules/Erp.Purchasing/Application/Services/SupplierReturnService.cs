using Erp.SeriesRegistry.Infrastructure.Application;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Common;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// Goods going back to suppliers. The mirror of a receipt: stock leaves the warehouse at the cost
/// it came in at, and only what is still in hand can go.
/// </summary>
/// <remarks>
/// The credit note that follows is a separate thing, recorded as a supplier document �?" the goods
/// leave here, the money is settled there. And goods that physically travel need a transport
/// document, which is fiscal and belongs to Sales as a delivery note of type <c>GD</c>.
/// </remarks>
public sealed class SupplierReturnService(
    ISupplierReturnStorage returnStorage,
    IGoodsReceiptStorage receiptStorage,
    IStockRecorder stockRecorder,
    IDocumentNumbers documentNumbers,
    IErpUnitOfWork unitOfWork) : ISupplierReturnService
{
    private const string NumberPrefix = "DEV";

    /// <summary>The document type the stock ledger records these movements under.</summary>
    private const string StockDocumentType = "DEV";

    public async Task<IReadOnlyList<SupplierReturnListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var returns = await returnStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        return [.. returns.Select(MapListItem)];
    }

    public async Task<SupplierReturnDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplierReturn = await returnStorage.GetByIdAsync(id, cancellationToken);
        return supplierReturn is null ? null : Map(supplierReturn);
    }

    public async Task<IReadOnlyList<ReturnableReceiptLineDto>> GetReturnableLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var live = receipts.Where(receipt => !receipt.IsVoided).ToList();

        var returned = await returnStorage.GetReturnedQuantitiesAsync(
            [.. live.SelectMany(receipt => receipt.Lines).Select(line => line.Id)],
            cancellationToken);

        return
        [
            .. live
                .SelectMany(receipt => receipt.Lines.Select(line => new
                {
                    Receipt = receipt,
                    Line = line,
                    Returned = returned.TryGetValue(line.Id, out var done) ? done : 0m
                }))
                .Where(x => x.Line.Quantity - x.Returned > 0)
                .Select(x => new ReturnableReceiptLineDto(
                    x.Receipt.Id,
                    x.Receipt.Number,
                    x.Receipt.ReceiptDate,
                    x.Receipt.SupplierId,
                    x.Receipt.Supplier.Name,
                    x.Receipt.WarehouseId,
                    x.Line.Id,
                    x.Line.ProductCode,
                    x.Line.ProductDescription,
                    x.Line.UnitOfMeasure,
                    x.Line.Quantity,
                    x.Returned,
                    x.Line.Quantity - x.Returned,
                    x.Line.UnitCost))
                .OrderByDescending(line => line.ReceiptDate)
                .ThenBy(line => line.ReceiptNumber, StringComparer.Ordinal)
        ];
    }

    public async Task<SupplierReturnDto> CreateAsync(
        CreateSupplierReturnRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);
        ArgumentNullException.ThrowIfNull(request.Lines);

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("A return has to say who the goods are going back to.", nameof(request));

        if (request.Lines.Count == 0)
            throw new ArgumentException("A return with no lines sends nothing back.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Inside the transaction, so the counter row stays locked while the number is taken.
        var number = await documentNumbers.NextAsync(
            request.CompanyId, NumberPrefix, request.ReturnDate.Year, cancellationToken);

        var receipts = await ResolveReceiptsAsync(request, cancellationToken);

        // Everything on one return leaves the same warehouse: the one the goods are sitting in.
        var warehouseId = receipts.Values.Select(receipt => receipt.WarehouseId).Distinct().Single();

        var supplierReturn = SupplierReturn.Create(
            request.CompanyId,
            request.SupplierId,
            ToSnapshot(request.Supplier),
            number,
            request.ReturnDate,
            warehouseId,
            request.Reason,
            [.. request.Lines.Select(line => ToLine(line, receipts))],
            request.Notes,
            userId);

        await returnStorage.AddAsync(supplierReturn, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await RecordStockAsync(supplierReturn, userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(supplierReturn);
    }

    public async Task<SupplierReturnDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var supplierReturn = await returnStorage.GetByIdAsync(id, cancellationToken);
        if (supplierReturn is null)
            return null;

        supplierReturn.Void(reason, userId, DateTime.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await stockRecorder.ReverseDocumentAsync(
            supplierReturn.Id, $"Anulação de {supplierReturn.Number}: {reason}", userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(supplierReturn);
    }

    /// <summary>
    /// Loads and locks the receipts, and refuses to send back more than is still in hand. Reading
    /// what is left without the lock is what would let two returns both pass.
    /// </summary>
    private async Task<Dictionary<Guid, GoodsReceipt>> ResolveReceiptsAsync(
        CreateSupplierReturnRequest request,
        CancellationToken cancellationToken)
    {
        var receiptLineIds = request.Lines.Select(line => line.ReceiptLineId).Distinct().ToList();
        var returned = await returnStorage.GetReturnedQuantitiesAsync(receiptLineIds, cancellationToken);

        var receipts = new Dictionary<Guid, GoodsReceipt>();
        var claimed = new Dictionary<Guid, decimal>();

        foreach (var line in request.Lines)
        {
            var receipt = receipts.Values.FirstOrDefault(x => x.Lines.Any(y => y.Id == line.ReceiptLineId));

            if (receipt is null)
            {
                receipt = await receiptStorage.GetForUpdateByLineAsync(line.ReceiptLineId, cancellationToken)
                    ?? throw new ArgumentException(
                        $"Receipt line '{line.ReceiptLineId}' was not found.", nameof(request));

                if (receipt.CompanyId != request.CompanyId)
                    throw new ArgumentException("The receipt does not belong to the requested company.", nameof(request));

                if (receipt.SupplierId != request.SupplierId)
                    throw new ArgumentException("The receipt belongs to another supplier.", nameof(request));

                if (receipt.IsVoided)
                    throw new InvalidOperationException($"Receipt '{receipt.Number}' is voided.");

                receipts[receipt.Id] = receipt;
            }

            var receiptLine = receipt.Lines.First(x => x.Id == line.ReceiptLineId);

            returned.TryGetValue(line.ReceiptLineId, out var already);
            claimed.TryGetValue(line.ReceiptLineId, out var inThisRequest);
            claimed[line.ReceiptLineId] = inThisRequest + line.Quantity;

            if (already + claimed[line.ReceiptLineId] > receiptLine.Quantity)
            {
                throw new InvalidOperationException(
                    $"Line '{receiptLine.ProductCode}' of receipt '{receipt.Number}' has only " +
                    $"{receiptLine.Quantity - already:0.###} left to send back.");
            }
        }

        // One return, one warehouse: goods sitting in two places do not leave on the same movement.
        if (receipts.Values.Select(receipt => receipt.WarehouseId).Distinct().Count() > 1)
        {
            throw new ArgumentException(
                "The lines come from more than one warehouse. Make one return per warehouse.",
                nameof(request));
        }

        return receipts;
    }

    /// <summary>Takes the goods out of the warehouse, at the cost they came in at.</summary>
    private async Task RecordStockAsync(SupplierReturn supplierReturn, string? userId, CancellationToken cancellationToken)
    {
        var request = new RecordDocumentStockRequest(
            supplierReturn.CompanyId,
            supplierReturn.WarehouseId,
            StockDirection.Out,
            supplierReturn.ReturnDate,
            StockDocumentType,
            supplierReturn.Number,
            supplierReturn.Id,
            [.. supplierReturn.Lines.Select(line => new DocumentStockLine(
                line.Id,
                line.ProductCode,
                line.ProductDescription,
                line.Quantity,
                UnitCost: line.UnitCost))]);

        await stockRecorder.RecordAsync(request, userId, cancellationToken);
    }

    private static SupplierReturnLine ToLine(
        SupplierReturnLineRequest request,
        IReadOnlyDictionary<Guid, GoodsReceipt> receipts)
    {
        var receipt = receipts.Values.First(x => x.Lines.Any(y => y.Id == request.ReceiptLineId));
        var receiptLine = receipt.Lines.First(x => x.Id == request.ReceiptLineId);

        return new SupplierReturnLine
        {
            ReceiptId = receipt.Id,
            ReceiptLineId = request.ReceiptLineId,
            ProductCode = receiptLine.ProductCode,
            ProductDescription = receiptLine.ProductDescription,
            Quantity = request.Quantity,
            UnitOfMeasure = receiptLine.UnitOfMeasure,
            UnitCost = receiptLine.UnitCost
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

    private static SupplierReturnListItemDto MapListItem(SupplierReturn supplierReturn) =>
        new(supplierReturn.Id,
            supplierReturn.Number,
            supplierReturn.Status.ToString(),
            supplierReturn.ReturnDate,
            supplierReturn.Supplier.Name,
            supplierReturn.Reason,
            supplierReturn.Lines.Count,
            supplierReturn.TotalCost,
            supplierReturn.IsVoided);

    private static SupplierReturnDto Map(SupplierReturn supplierReturn) =>
        new(supplierReturn.Id,
            supplierReturn.CompanyId,
            supplierReturn.SupplierId,
            supplierReturn.Number,
            supplierReturn.Status.ToString(),
            supplierReturn.ReturnDate,
            supplierReturn.WarehouseId,
            supplierReturn.Reason,
            new PurchaseOrderSupplierDto(
                supplierReturn.Supplier.Code,
                supplierReturn.Supplier.Name,
                supplierReturn.Supplier.TaxId,
                supplierReturn.Supplier.Address,
                supplierReturn.Supplier.PostalCode,
                supplierReturn.Supplier.City,
                supplierReturn.Supplier.Country),
            supplierReturn.Notes,
            supplierReturn.TotalCost,
            supplierReturn.CreatedAtUtc,
            supplierReturn.VoidedAtUtc,
            supplierReturn.VoidReason,
            [.. supplierReturn.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SupplierReturnLineDto(
                    line.Id,
                    line.LineNumber,
                    line.ReceiptId,
                    line.ReceiptLineId,
                    line.ProductCode,
                    line.ProductDescription,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitCost,
                    line.LineAmount))]);
}
