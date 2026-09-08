using Erp.Common;
using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.QrCode;
using Erp.FiscalPT.Signing;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.SeriesRegistry.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// Issues invoices in a supplier's name. This is the one place in Purchasing that behaves like
/// <c>Erp.Sales</c>, and for the only reason that matters: we are the ones issuing.
/// </summary>
/// <remarks>
/// The QR code says what the document is. Field A is the issuer's tax id, which is <b>ours</b> —
/// the program that signed it is ours — and field B is the tax id of the other party, the supplier.
/// It is the mirror image of a sale, and it is what makes the same signature machinery correct here.
/// </remarks>
public sealed class SelfBilledInvoiceService(
    ISelfBilledInvoiceStorage invoiceStorage,
    IGoodsReceiptStorage receiptStorage,
    ISeriesStorage seriesStorage,
    IDocumentSigner signer,
    IUnitOfWork unitOfWork,
    IOptions<FiscalOptions> fiscalOptions) : ISelfBilledInvoiceService
{
    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<IReadOnlyList<SelfBilledInvoiceListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await invoiceStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        return [.. invoices.Select(MapListItem)];
    }

    public async Task<SelfBilledInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        return invoice is null ? null : Map(invoice);
    }

    public async Task<IReadOnlyList<UnbilledReceiptLineDto>> GetUnbilledReceiptLinesAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var receipts = await receiptStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var live = receipts.Where(receipt => !receipt.IsVoided).ToList();

        var billed = await invoiceStorage.GetSelfBilledQuantitiesAsync(
            [.. live.SelectMany(receipt => receipt.Lines).Select(line => line.Id)],
            cancellationToken);

        return
        [
            .. live
                .SelectMany(receipt => receipt.Lines.Select(line => new
                {
                    Receipt = receipt,
                    Line = line,
                    Pending = line.Quantity - (billed.TryGetValue(line.Id, out var done) ? done : 0m)
                }))
                .Where(x => x.Pending > 0)
                .Select(x => new UnbilledReceiptLineDto(
                    x.Receipt.Id,
                    x.Receipt.Number,
                    x.Receipt.ReceiptDate,
                    x.Receipt.SupplierId,
                    x.Receipt.Supplier.Name,
                    x.Line.Id,
                    x.Line.ProductCode,
                    x.Line.ProductDescription,
                    x.Line.UnitOfMeasure,
                    x.Line.Quantity,
                    x.Line.Quantity - x.Pending,
                    x.Pending,
                    x.Line.UnitCost))
                .OrderBy(line => line.ReceiptDate)
                .ThenBy(line => line.ReceiptNumber, StringComparer.Ordinal)
        ];
    }

    public async Task<SelfBilledInvoiceDto> IssueAsync(
        IssueSelfBilledInvoiceRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("A document must have at least one line.", nameof(request));

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("A self-billed invoice has to say whose sale it titles.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The series row is locked for the whole transaction: without it, two concurrent issues
        // could take the same sequence number or chain onto the same previous hash.
        var series = await seriesStorage.GetForUpdateAsync(request.SeriesId, cancellationToken)
            ?? throw new ArgumentException($"Series '{request.SeriesId}' was not found.", nameof(request));

        if (series.CompanyId != request.CompanyId)
            throw new ArgumentException("The series does not belong to the requested company.", nameof(request));

        // The whole point of the flag. Numbering these from an ordinary sales series would mix our
        // own sales with the supplier's into one chain, and into one SAF-T file.
        if (!series.SelfBilling)
        {
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' is not a self-billing series.",
                nameof(request));
        }

        if (!SalesDocumentTypes.IsSupported(series.DocumentType))
        {
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' is for '{series.DocumentType}' documents, not for invoicing.",
                nameof(request));
        }

        if (!series.CanIssue)
        {
            throw new InvalidOperationException(
                $"Series '{series.SeriesCode}' has no validation code from the tax authority yet, so it cannot issue documents.");
        }

        // Locks the receipts being billed, so two documents cannot bill the same delivery at the
        // same time, each blind to the other.
        var origins = await ResolveReceiptOriginsAsync(request, cancellationToken);

        var lines = BuildLines(request.Lines, origins);
        var taxSummaries = BuildTaxSummaries(lines);

        var netTotal = FiscalRounding.Amount(lines.Sum(x => x.LineAmount));
        var taxPayable = FiscalRounding.Amount(lines.Sum(x => x.TaxAmount));
        var grossTotal = FiscalRounding.Amount(netTotal + taxPayable);

        var sequenceNumber = series.TakeNextSequence();
        var documentNumber = DocumentNumber.Build(series.DocumentType, series.SeriesCode, sequenceNumber);
        var atcud = Atcud.Build(series.ValidationCode!, sequenceNumber);

        // Seconds precision, because that is what goes into the signed string.
        var systemEntryDateUtc = TruncateToSeconds(DateTime.UtcNow);
        var previousHash = await invoiceStorage.GetLastHashAsync(series.Id, cancellationToken);

        var signature = signer.Sign(request.IssueDate, systemEntryDateUtc, documentNumber, grossTotal, previousHash);

        var invoice = SelfBilledInvoice.Issue(
            request.CompanyId,
            request.SupplierId,
            series,
            sequenceNumber,
            documentNumber,
            atcud,
            request.IssueDate,
            systemEntryDateUtc,
            ToSnapshot(request.Supplier),
            lines,
            taxSummaries,
            netTotal,
            taxPayable,
            grossTotal,
            signature.Hash,
            previousHash,
            signature.HashControl,
            userId,
            request.SupplierAgreementReference?.Trim());

        invoice.AttachQrCodePayload(BuildQrCodePayload(invoice, signature.Hash));

        await invoiceStorage.AddAsync(invoice, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Map(invoice);
    }

    public async Task<SelfBilledInvoiceDto?> AcceptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
            return null;

        invoice.Accept(DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(invoice);
    }

    public async Task<SelfBilledInvoiceDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        var invoice = await invoiceStorage.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
            return null;

        var statusChange = invoice.Void(reason, userId, DateTime.UtcNow);

        // No transaction and no stock to put back: this document never moved any. What it undoes is
        // the billing of the receipt lines, which is derived from the status, not stored.
        await invoiceStorage.AddStatusChangeAsync(statusChange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(invoice);
    }

    /// <summary>
    /// The goods receipt lines this document draws from, and what is left of each. Reading them
    /// under a lock is what stops two documents from billing the same delivery twice.
    /// </summary>
    private async Task<Dictionary<Guid, ReceiptOrigin>> ResolveReceiptOriginsAsync(
        IssueSelfBilledInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var lineIds = request.Lines
            .Where(line => line.ReceiptLineId is not null)
            .Select(line => line.ReceiptLineId!.Value)
            .Distinct()
            .ToList();

        var origins = new Dictionary<Guid, ReceiptOrigin>();

        if (lineIds.Count == 0)
            return origins;

        var billed = await invoiceStorage.GetSelfBilledQuantitiesAsync(lineIds, cancellationToken);

        foreach (var lineId in lineIds)
        {
            var receipt = await receiptStorage.GetForUpdateByLineAsync(lineId, cancellationToken)
                ?? throw new ArgumentException($"Goods receipt line '{lineId}' was not found.", nameof(request));

            if (receipt.CompanyId != request.CompanyId)
                throw new ArgumentException("A goods receipt of another company cannot be billed.", nameof(request));

            // Billing one supplier for another's delivery would put the wrong sale in the wrong file.
            if (receipt.SupplierId != request.SupplierId)
            {
                throw new ArgumentException(
                    $"Goods receipt '{receipt.Number}' is from another supplier.",
                    nameof(request));
            }

            if (receipt.IsVoided)
            {
                throw new ArgumentException(
                    $"Goods receipt '{receipt.Number}' is voided, so it cannot be billed.",
                    nameof(request));
            }

            var line = receipt.Lines.First(x => x.Id == lineId);
            var already = billed.TryGetValue(lineId, out var quantity) ? quantity : 0m;

            origins[lineId] = new ReceiptOrigin(receipt.Id, receipt.Number, line.Quantity - already);
        }

        return origins;
    }

    /// <summary>A goods receipt line this document is drawing from, and what is left of it.</summary>
    private sealed record ReceiptOrigin(Guid ReceiptId, string Number, decimal PendingQuantity);

    private static List<SelfBilledInvoiceLine> BuildLines(
        IReadOnlyList<SelfBilledInvoiceLineRequest> requestLines,
        Dictionary<Guid, ReceiptOrigin> origins)
    {
        var lines = new List<SelfBilledInvoiceLine>(requestLines.Count);
        var lineNumber = 1;

        foreach (var line in requestLines)
        {
            if (line.Quantity <= 0)
                throw new ArgumentException($"Line {lineNumber} must have a positive quantity.", nameof(requestLines));

            if (line.UnitPrice < 0)
                throw new ArgumentException($"Line {lineNumber} cannot have a negative unit price.", nameof(requestLines));

            if (!TaxCodes.All.Contains(line.TaxCode, StringComparer.Ordinal))
                throw new ArgumentException($"Line {lineNumber} has an unknown tax code '{line.TaxCode}'.", nameof(requestLines));

            if (string.Equals(line.TaxCode, TaxCodes.Exempt, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(line.TaxExemptionReason))
            {
                throw new ArgumentException($"Line {lineNumber} is exempt and requires an exemption reason.", nameof(requestLines));
            }

            ReceiptOrigin? origin = null;

            if (line.ReceiptLineId is { } receiptLineId)
            {
                origin = origins[receiptLineId];

                if (line.Quantity > origin.PendingQuantity)
                {
                    throw new ArgumentException(
                        $"Line {lineNumber} bills {line.Quantity:0.######} of '{origin.Number}', which only has {origin.PendingQuantity:0.######} left to bill.",
                        nameof(requestLines));
                }
            }

            var lineAmount = FiscalRounding.Amount(line.Quantity * line.UnitPrice);
            var taxAmount = FiscalRounding.Amount(lineAmount * line.TaxPercentage / 100m);

            lines.Add(new SelfBilledInvoiceLine
            {
                LineNumber = lineNumber,
                ReceiptLineId = line.ReceiptLineId,
                ReceiptId = origin?.ReceiptId ?? line.ReceiptId,
                ProductCode = line.ProductCode,
                ProductDescription = line.ProductDescription,
                Quantity = line.Quantity,
                UnitOfMeasure = line.UnitOfMeasure,
                UnitPrice = line.UnitPrice,
                LineAmount = lineAmount,
                TaxCountryRegion = line.TaxCountryRegion,
                TaxCode = line.TaxCode,
                TaxPercentage = line.TaxPercentage,
                TaxAmount = taxAmount,
                TaxExemptionCode = line.TaxExemptionCode,
                TaxExemptionReason = line.TaxExemptionReason
            });

            lineNumber++;
        }

        return lines;
    }

    private static List<SelfBilledInvoiceTaxSummary> BuildTaxSummaries(IReadOnlyList<SelfBilledInvoiceLine> lines) =>
        [
            .. lines
                .GroupBy(x => new { x.TaxCountryRegion, x.TaxCode, x.TaxPercentage })
                .Select(group => new SelfBilledInvoiceTaxSummary
                {
                    TaxCountryRegion = group.Key.TaxCountryRegion,
                    TaxCode = group.Key.TaxCode,
                    TaxPercentage = group.Key.TaxPercentage,
                    TaxableBase = FiscalRounding.Amount(group.Sum(x => x.LineAmount)),
                    TaxAmount = FiscalRounding.Amount(group.Sum(x => x.TaxAmount))
                })
        ];

    private string BuildQrCodePayload(SelfBilledInvoice invoice, string hash)
    {
        var fields = new QrCodeFields
        {
            // Ours: the program that signed it is ours, whoever the sale belongs to.
            IssuerTaxId = _fiscal.IssuerTaxId,
            BuyerTaxId = invoice.Supplier.TaxId,
            BuyerCountry = invoice.Supplier.Country,
            DocumentType = invoice.DocumentType,
            DocumentStatus = invoice.EffectiveStatus,
            DocumentDate = invoice.IssueDate,
            DocumentNumber = invoice.DocumentNumber,
            Atcud = invoice.Atcud,
            TaxAmounts = invoice.TaxSummaries
                .Select(x => new QrCodeTaxAmount(x.TaxCountryRegion, x.TaxCode, x.TaxableBase, x.TaxAmount))
                .ToList(),
            TotalTaxes = invoice.TaxPayable,
            GrossTotal = invoice.GrossTotal,
            HashCharacters = RsaDocumentSigner.ExtractPrintableHash(hash),
            CertificateNumber = _fiscal.CertificateNumber
        };

        return QrCodePayloadBuilder.Build(fields);
    }

    private static SupplierSnapshot ToSnapshot(PurchaseOrderSupplierDto supplier) =>
        new(supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country);

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);

    private static SelfBilledInvoiceListItemDto MapListItem(SelfBilledInvoice invoice) =>
        new(invoice.Id,
            invoice.DocumentNumber,
            invoice.DocumentType,
            invoice.Atcud,
            invoice.IssueDate,
            invoice.Supplier.Name,
            invoice.Supplier.TaxId,
            invoice.NetTotal,
            invoice.TaxPayable,
            invoice.GrossTotal,
            invoice.EffectiveStatus,
            invoice.IsAccepted);

    private static SelfBilledInvoiceDto Map(SelfBilledInvoice invoice) =>
        new(invoice.Id,
            invoice.CompanyId,
            invoice.SupplierId,
            invoice.SeriesId,
            invoice.DocumentType,
            invoice.DocumentNumber,
            invoice.Atcud,
            invoice.IssueDate,
            invoice.SystemEntryDateUtc,
            invoice.EffectiveStatus,
            invoice.AcceptedBySupplierAtUtc,
            new PurchaseOrderSupplierDto(
                invoice.Supplier.Code,
                invoice.Supplier.Name,
                invoice.Supplier.TaxId,
                invoice.Supplier.Address,
                invoice.Supplier.PostalCode,
                invoice.Supplier.City,
                invoice.Supplier.Country),
            invoice.NetTotal,
            invoice.TaxPayable,
            invoice.GrossTotal,
            RsaDocumentSigner.ExtractPrintableHash(invoice.Hash),
            invoice.HashControl,
            invoice.QrCodePayload,
            [
                .. invoice.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line => new SelfBilledInvoiceLineDto(
                        line.Id,
                        line.LineNumber,
                        line.ReceiptLineId,
                        line.ReceiptId,
                        line.ProductCode,
                        line.ProductDescription,
                        line.Quantity,
                        line.UnitOfMeasure,
                        line.UnitPrice,
                        line.LineAmount,
                        line.TaxCountryRegion,
                        line.TaxCode,
                        line.TaxPercentage,
                        line.TaxAmount,
                        line.TaxExemptionCode,
                        line.TaxExemptionReason))
            ],
            [
                .. invoice.TaxSummaries.Select(summary => new SelfBilledInvoiceTaxSummaryDto(
                    summary.TaxCountryRegion,
                    summary.TaxCode,
                    summary.TaxPercentage,
                    summary.TaxableBase,
                    summary.TaxAmount))
            ]);
}
