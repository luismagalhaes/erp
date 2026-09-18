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
/// Supplier invoices, written into our books. Nothing here is issued by us: the number, the date
/// and the ATCUD are the supplier's, recorded as they came.
/// </summary>
/// <remarks>
/// Two rules carry the weight. The same document from the same supplier may only be recorded once,
/// because recording it twice deducts the VAT twice. And a line that comes from a goods receipt
/// moves no stock, because the receipt already moved it — the integrating-document rule, the same
/// one that stops an invoice from re-moving what a delivery note moved on the sales side.
/// </remarks>
public sealed class PurchaseInvoiceService(
    IPurchaseInvoiceStorage invoiceStorage,
    IGoodsReceiptStorage receiptStorage,
    IStockRecorder stockRecorder,
    ISupplierPaymentStorage paymentStorage,
    IUnitOfWork unitOfWork) : IPurchaseInvoiceService
{
    /// <summary>The document type the stock ledger records these movements under.</summary>
    private const string StockDocumentType = "FTF";

    public async Task<IReadOnlyList<PurchaseInvoiceListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await invoiceStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        return [.. invoices.Select(MapListItem)];
    }

    public IQueryable<PurchaseInvoiceListItemDto> Query(Guid companyId) => invoiceStorage.Query(companyId);

    public async Task<PurchaseInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        return invoice is null ? null : Map(invoice);
    }

    public async Task<IReadOnlyList<UninvoicedReceiptLineDto>> GetUninvoicedReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var live = receipts.Where(receipt => !receipt.IsVoided).ToList();

        var invoiced = await invoiceStorage.GetInvoicedQuantitiesAsync(
            [.. live.SelectMany(receipt => receipt.Lines).Select(line => line.Id)],
            cancellationToken: cancellationToken);

        return
        [
            .. live
                .SelectMany(receipt => receipt.Lines.Select(line => new
                {
                    Receipt = receipt,
                    Line = line,
                    Pending = line.Quantity - (invoiced.TryGetValue(line.Id, out var done) ? done : 0m)
                }))
                .Where(x => x.Pending > 0)
                .Select(x => new UninvoicedReceiptLineDto(
                    x.Receipt.Id,
                    x.Receipt.Number,
                    x.Receipt.ReceiptDate,
                    x.Receipt.SupplierDocumentNumber,
                    x.Receipt.SupplierId,
                    x.Receipt.Supplier.Name,
                    x.Line.Id,
                    x.Line.OrderLineId,
                    x.Line.ProductCode,
                    x.Line.ProductDescription,
                    x.Line.UnitOfMeasure,
                    x.Line.Quantity,
                    x.Line.Quantity - x.Pending,
                    x.Pending,
                    x.Line.UnitCost,
                    x.Line.DiscountPercentage))
                .OrderBy(line => line.ReceiptDate)
                .ThenBy(line => line.ReceiptNumber, StringComparer.Ordinal)
        ];
    }

    public async Task<PurchaseInvoiceDto> RecordAsync(
        RecordPurchaseInvoiceRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);
        ArgumentNullException.ThrowIfNull(request.Lines);

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("An invoice has to say who issued it.", nameof(request));

        if (request.Lines.Count == 0)
            throw new ArgumentException("An invoice with no lines records nothing.", nameof(request));

        // Checked here for a message worth reading; the unique index refuses it regardless.
        if (await invoiceStorage.ExistsAsync(
                request.CompanyId, request.Supplier.TaxId, request.SupplierDocumentNumber, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Document '{request.SupplierDocumentNumber}' from this supplier is already recorded. " +
                "Recording it twice would deduct the VAT twice.");
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var receipts = await ResolveReceiptsAsync(request.CompanyId, request.SupplierId, request.Lines, cancellationToken);

        var invoice = PurchaseInvoice.Create(
            request.CompanyId,
            request.SupplierId,
            ToSnapshot(request.Supplier),
            request.DocumentType,
            request.SupplierDocumentNumber,
            request.SupplierDocumentDate,
            request.ReceivedDate,
            [.. request.Lines.Select(line => ToLine(line, receipts))],
            request.WarehouseId,
            request.SupplierAtcud,
            request.DueDate,
            request.ReverseCharge,
            request.Notes,
            userId);

        await invoiceStorage.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await RecordStockAsync(invoice, userId, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(invoice);
    }

    public async Task<PurchaseInvoiceDto?> UpdateAsync(
        Guid id,
        UpdatePurchaseInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Lines);

        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
            return null;

        var receipts = await ResolveReceiptsAsync(
            invoice.CompanyId, invoice.SupplierId, request.Lines, cancellationToken, invoice.Id);

        invoice.UpdateHeader(
            request.SupplierDocumentDate,
            request.ReceivedDate,
            request.DueDate,
            request.SupplierAtcud,
            request.ReverseCharge,
            request.Notes);

        invoice.ReplaceLines([.. request.Lines.Select(line => ToLine(line, receipts))], request.WarehouseId);

        // Correcting the figures must not leave the supplier paid more than the invoice now says.
        var paid = await GetPaidAmountAsync(invoice.Id, cancellationToken);

        if (paid > invoice.GrossTotal)
        {
            throw new InvalidOperationException(
                $"Invoice '{invoice.SupplierDocumentNumber}' is already paid {paid:0.00}, more than its new total of {invoice.GrossTotal:0.00}. Void the payment first.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(invoice);
    }

    public async Task<PurchaseInvoiceDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
            return null;

        // A paid invoice cannot simply disappear from the current account: the money is still out.
        if (await GetPaidAmountAsync(invoice.Id, cancellationToken) > 0)
        {
            throw new InvalidOperationException(
                $"Invoice '{invoice.SupplierDocumentNumber}' has payments against it. Void them first.");
        }

        var movedStock = invoice.MovedStock;
        invoice.Void(reason, userId, DateTime.UtcNow);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Only what this document brought in comes back out. Goods a receipt brought in stay: that
        // receipt is still standing, and undoing it is its own decision.
        if (movedStock)
        {
            await stockRecorder.ReverseDocumentAsync(
                invoice.Id,
                $"Anulação do registo de {invoice.SupplierDocumentNumber}: {reason}",
                userId,
                cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return Map(invoice);
    }

    private async Task<decimal> GetPaidAmountAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var paid = await paymentStorage.GetPaidAmountsAsync([invoiceId], cancellationToken);
        return paid.TryGetValue(invoiceId, out var amount) ? amount : 0m;
    }

    /// <summary>
    /// Loads the receipts the invoice lines point at, and refuses to invoice more of a receipt line
    /// than it received. What is already invoiced is derived from the invoice lines, so the receipt
    /// never has to be rewritten �?" the same shape as invoicing from delivery notes in Sales.
    /// </summary>
    /// <param name="excludeInvoiceId">
    /// The invoice being rewritten, whose own lines must not count against its own ceiling.
    /// </param>
    private async Task<Dictionary<Guid, GoodsReceipt>> ResolveReceiptsAsync(
        Guid companyId,
        Guid supplierId,
        IReadOnlyList<PurchaseInvoiceLineRequest> lines,
        CancellationToken cancellationToken,
        Guid? excludeInvoiceId = null)
    {
        var receiptLineIds = lines
            .Where(line => line.ReceiptLineId is not null)
            .Select(line => line.ReceiptLineId!.Value)
            .Distinct()
            .ToList();

        var receipts = new Dictionary<Guid, GoodsReceipt>();

        if (receiptLineIds.Count == 0)
            return receipts;

        var invoiced = await invoiceStorage.GetInvoicedQuantitiesAsync(
            receiptLineIds, excludeInvoiceId, cancellationToken);

        var claimed = new Dictionary<Guid, decimal>();

        foreach (var line in lines.Where(line => line.ReceiptLineId is not null))
        {
            var receiptLineId = line.ReceiptLineId!.Value;

            var receipt = receipts.Values.FirstOrDefault(x => x.Lines.Any(y => y.Id == receiptLineId));

            if (receipt is null)
            {
                receipt = await receiptStorage.GetForUpdateByLineAsync(receiptLineId, cancellationToken)
                    ?? throw new ArgumentException($"Receipt line '{receiptLineId}' was not found.", nameof(lines));

                if (receipt.CompanyId != companyId)
                    throw new ArgumentException("The receipt does not belong to the requested company.", nameof(lines));

                if (receipt.SupplierId != supplierId)
                    throw new ArgumentException("The receipt belongs to another supplier.", nameof(lines));

                if (receipt.IsVoided)
                    throw new InvalidOperationException($"Receipt '{receipt.Number}' is voided.");

                receipts[receipt.Id] = receipt;
            }

            var receiptLine = receipt.Lines.First(x => x.Id == receiptLineId);

            invoiced.TryGetValue(receiptLineId, out var already);

            claimed.TryGetValue(receiptLineId, out var inThisRequest);
            claimed[receiptLineId] = inThisRequest + line.Quantity;

            if (already + claimed[receiptLineId] > receiptLine.Quantity)
            {
                throw new InvalidOperationException(
                    $"Line '{receiptLine.ProductCode}' of receipt '{receipt.Number}' has only " +
                    $"{receiptLine.Quantity - already:0.###} left to invoice.");
            }
        }

        return receipts;
    }

    /// <summary>
    /// Brings in the goods this document carries that no receipt already brought. Every line goes
    /// to the recorder with the receipt line as its origin, so the ones already moved are skipped
    /// rather than refused �?" an invoice following a receipt is normal, it just has nothing to move.
    /// </summary>
    private async Task RecordStockAsync(PurchaseInvoice invoice, string? userId, CancellationToken cancellationToken)
    {
        if (invoice.WarehouseId is not { } warehouseId)
            return;

        var goods = invoice.Lines
            .Where(line => line.DeductionNature == DeductionNature.Inventory)
            .ToList();

        if (goods.Count == 0)
            return;

        var request = new RecordDocumentStockRequest(
            invoice.CompanyId,
            warehouseId,
            StockDirection.In,
            invoice.SupplierDocumentDate,
            StockDocumentType,
            invoice.SupplierDocumentNumber,
            invoice.Id,
            [.. goods.Select(line => new DocumentStockLine(
                line.Id,
                line.ProductCode,
                line.ProductDescription,
                line.Quantity,
                line.ReceiptLineId,
                // What the goods actually cost, discount included \u2014 the receipt already did this
                // for lines that moved stock through it.
                line.Quantity == 0 ? 0m : FiscalRounding.Amount(line.LineAmount / line.Quantity)))]);

        await stockRecorder.RecordAsync(request, userId, cancellationToken);
    }

    private static PurchaseInvoiceLine ToLine(
        PurchaseInvoiceLineRequest request,
        IReadOnlyDictionary<Guid, GoodsReceipt> receipts)
    {
        var receipt = request.ReceiptLineId is { } receiptLineId
            ? receipts.Values.FirstOrDefault(x => x.Lines.Any(y => y.Id == receiptLineId))
            : null;

        var receiptLine = receipt?.Lines.FirstOrDefault(x => x.Id == request.ReceiptLineId);

        // The receipt line is the source document here: its discount is what the invoice inherits
        // unless the caller overrides it — the integrating-document rule applied to money as well
        // as quantity.
        var discountPercentage = request.DiscountPercentage != 0m
            ? request.DiscountPercentage
            : receiptLine?.DiscountPercentage ?? 0m;

        return new PurchaseInvoiceLine
        {
            ReceiptId = receipt?.Id,
            ReceiptLineId = request.ReceiptLineId,
            ReturnLineId = request.ReturnLineId,
            OrderLineId = receiptLine?.OrderLineId,
            ProductCode = request.ProductCode.Trim(),
            ProductDescription = request.ProductDescription.Trim(),
            Quantity = request.Quantity,
            UnitOfMeasure = request.UnitOfMeasure,
            UnitPrice = request.UnitPrice,
            DiscountPercentage = discountPercentage,
            TaxCountryRegion = request.TaxCountryRegion,
            TaxCode = request.TaxCode,
            TaxPercentage = request.TaxPercentage,
            DeductionNature = ParseNature(request.DeductionNature)
        };
    }

    private static DeductionNature ParseNature(string value) =>
        Enum.TryParse<DeductionNature>(value, ignoreCase: true, out var nature)
            ? nature
            : throw new ArgumentException($"Unknown deduction nature '{value}'.", nameof(value));

    private static SupplierSnapshot ToSnapshot(PurchaseOrderSupplierDto supplier) =>
        new(supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country);

    private static PurchaseInvoiceListItemDto MapListItem(PurchaseInvoice invoice) =>
        new()
        {
            Id = invoice.Id,
            DocumentType = invoice.DocumentType,
            SupplierDocumentNumber = invoice.SupplierDocumentNumber,
            SupplierDocumentDate = invoice.SupplierDocumentDate,
            ReceivedDate = invoice.ReceivedDate,
            DueDate = invoice.DueDate,
            SupplierName = invoice.Supplier.Name,
            SupplierTaxId = invoice.Supplier.TaxId,
            NetTotal = invoice.NetTotal,
            TaxTotal = invoice.TaxTotal,
            GrossTotal = invoice.GrossTotal,
            ReverseCharge = invoice.ReverseCharge,
            Status = invoice.Status.ToString(),
            IsVoided = invoice.IsVoided
        };

    private static PurchaseInvoiceDto Map(PurchaseInvoice invoice) =>
        new(invoice.Id,
            invoice.CompanyId,
            invoice.SupplierId,
            invoice.DocumentType,
            invoice.SupplierDocumentNumber,
            invoice.SupplierDocumentDate,
            invoice.SupplierAtcud,
            invoice.ReceivedDate,
            invoice.DueDate,
            invoice.ReverseCharge,
            invoice.Status.ToString(),
            invoice.WarehouseId,
            new PurchaseOrderSupplierDto(
                invoice.Supplier.Code,
                invoice.Supplier.Name,
                invoice.Supplier.TaxId,
                invoice.Supplier.Address,
                invoice.Supplier.PostalCode,
                invoice.Supplier.City,
                invoice.Supplier.Country),
            invoice.Notes,
            invoice.NetTotal,
            invoice.TaxTotal,
            invoice.GrossTotal,
            invoice.CreatedAtUtc,
            invoice.VoidedAtUtc,
            invoice.VoidReason,
            invoice.MovedStock,
            [.. invoice.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new PurchaseInvoiceLineDto(
                    line.Id,
                    line.LineNumber,
                    line.ReceiptId,
                    line.ReceiptLineId,
                    line.OrderLineId,
                    line.ReturnLineId,
                    line.ProductCode,
                    line.ProductDescription,
                    line.Quantity,
                    line.UnitOfMeasure,
                    line.UnitPrice,
                    line.DiscountPercentage,
                    line.DiscountAmount,
                    line.LineAmount,
                    line.TaxCountryRegion,
                    line.TaxCode,
                    line.TaxPercentage,
                    line.TaxAmount,
                    line.DeductionNature.ToString(),
                    line.MovesStock))],
            [.. invoice.TaxSummary
                .OrderBy(summary => summary.TaxCode, StringComparer.Ordinal)
                .Select(summary => new PurchaseInvoiceTaxSummaryDto(
                    summary.TaxCountryRegion,
                    summary.TaxCode,
                    summary.TaxPercentage,
                    summary.TaxableBase,
                    summary.TaxAmount))]);
}
