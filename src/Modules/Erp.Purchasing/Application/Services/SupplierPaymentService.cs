using Erp.Common;
using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Application;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.SeriesRegistry.Infrastructure.Application;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// Payments to suppliers — the supplier side of the current account, and the mirror of the receipts
/// in <c>Erp.Sales</c>. The rules are the same ones: a payment never settles more than a document
/// still owes, never settles a voided document, and its payment methods add up to exactly what it
/// settles. What is not the same is that nothing here is fiscal: no series, no signature, no SAF-T.
/// </summary>
public sealed class SupplierPaymentService(
    ISupplierPaymentStorage paymentStorage,
    IPurchaseInvoiceStorage invoiceStorage,
    ISelfBilledInvoiceStorage selfBilledStorage,
    IDocumentNumbers documentNumbers,
    IUnitOfWork unitOfWork) : ISupplierPaymentService
{
    /// <summary>Prefix of the payment number. Ours, with no fiscal meaning.</summary>
    private const string NumberPrefix = "PAG";

    public async Task<IReadOnlyList<SupplierPaymentListItemDto>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var payments = await paymentStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        return [.. payments.Select(MapListItem)];
    }

    public IQueryable<SupplierPaymentListItemDto> Query(Guid companyId) => paymentStorage.Query(companyId);

    public async Task<SupplierPaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await paymentStorage.GetByIdAsync(id, cancellationToken);
        return payment is null ? null : Map(payment);
    }

    public async Task<IReadOnlyList<PayableDocumentDto>> GetPayableDocumentsAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        var invoices = await invoiceStorage.GetAllAsync(companyId, supplierId, cancellationToken);
        var selfBilled = await selfBilledStorage.GetAllAsync(companyId, supplierId, cancellationToken);

        var candidates = new List<PayableDocumentDto>();

        // Credit notes are listed too: their credit is what a payment takes off the invoices.
        candidates.AddRange(invoices
            .Where(invoice => !invoice.IsVoided && IsOpenInCurrentAccount(invoice.DocumentType))
            .Select(invoice => new PayableDocumentDto(
                nameof(PayableDocumentKind.PurchaseInvoice),
                invoice.Id,
                invoice.DocumentType,
                invoice.SupplierDocumentNumber,
                invoice.SupplierDocumentDate,
                invoice.DueDate,
                invoice.SupplierId,
                invoice.Supplier.Name,
                invoice.Supplier.TaxId,
                invoice.IsCredit,
                invoice.GrossTotal,
                0m,
                invoice.GrossTotal)));

        candidates.AddRange(selfBilled
            .Where(invoice => !invoice.IsVoided && IsOpenInCurrentAccount(invoice.DocumentType))
            .Select(invoice => new PayableDocumentDto(
                nameof(PayableDocumentKind.SelfBilledInvoice),
                invoice.Id,
                invoice.DocumentType,
                invoice.DocumentNumber,
                invoice.IssueDate,
                null,
                invoice.SupplierId,
                invoice.Supplier.Name,
                invoice.Supplier.TaxId,
                PurchaseDocumentTypes.IsCredit(invoice.DocumentType),
                invoice.GrossTotal,
                0m,
                invoice.GrossTotal)));

        var paid = await paymentStorage.GetPaidAmountsAsync(
            [.. candidates.Select(document => document.DocumentId)], cancellationToken);

        return
        [
            .. candidates
                .Select(document =>
                {
                    var alreadyPaid = paid.TryGetValue(document.DocumentId, out var amount) ? amount : 0m;
                    return document with
                    {
                        PaidAmount = alreadyPaid,
                        OutstandingAmount = FiscalRounding.Amount(document.GrossTotal - alreadyPaid)
                    };
                })
                .Where(document => document.OutstandingAmount > 0)
                .OrderBy(document => document.IsCredit)
                .ThenBy(document => document.DueDate ?? document.DocumentDate)
                .ThenBy(document => document.DocumentDate)
        ];
    }

    public async Task<SupplierPaymentDto> RecordAsync(
        CreateSupplierPaymentRequest request,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Supplier);

        if (request.SupplierId == Guid.Empty)
            throw new ArgumentException("A payment has to say which supplier it is paying.", nameof(request));

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("A payment must settle at least one document.", nameof(request));

        // No methods is legitimate when credit notes cover the invoices entirely; the payment itself
        // decides, once it knows its total.
        ArgumentNullException.ThrowIfNull(request.Methods);

        // The counter row stays locked until the payment is written. Besides keeping the numbering
        // whole, it serialises the company's payments of that year, so two of them cannot both read
        // the same outstanding amount and together pay a document twice.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var number = await documentNumbers.NextAsync(
            request.CompanyId, NumberPrefix, request.PaymentDate.Year, cancellationToken);

        var lines = await BuildLinesAsync(request, cancellationToken);
        var methods = BuildMethods(request.Methods);

        var payment = SupplierPayment.Create(
            request.CompanyId,
            request.SupplierId,
            ToSnapshot(request.Supplier),
            number,
            request.PaymentDate,
            lines,
            methods,
            request.Description,
            userId);

        await paymentStorage.AddAsync(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(payment);
    }

    public async Task<SupplierPaymentDto?> VoidAsync(
        Guid id,
        string reason,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var payment = await paymentStorage.GetByIdAsync(id, cancellationToken);
        if (payment is null)
            return null;

        payment.Void(reason, userId, DateTime.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(payment);
    }

    /// <summary>
    /// Turns the requested settlements into lines, checking that each document exists, is this
    /// supplier's, is not voided, and still owes at least what is being applied to it.
    /// </summary>
    private async Task<List<SupplierPaymentLine>> BuildLinesAsync(
        CreateSupplierPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var documentIds = request.Lines.Select(line => line.DocumentId).Distinct().ToList();

        if (documentIds.Count != request.Lines.Count)
            throw new ArgumentException("The same document cannot be settled twice on one payment.", nameof(request));

        var paid = await paymentStorage.GetPaidAmountsAsync(documentIds, cancellationToken);
        var lines = new List<SupplierPaymentLine>(request.Lines.Count);
        var lineNumber = 1;

        foreach (var line in request.Lines)
        {
            if (line.AppliedAmount <= 0)
                throw new ArgumentException($"Line {lineNumber} must settle a positive amount.", nameof(request));

            var document = await ResolveDocumentAsync(line, cancellationToken);

            if (document.CompanyId != request.CompanyId)
                throw new ArgumentException("The document does not belong to the requested company.", nameof(request));

            if (document.SupplierId != request.SupplierId)
            {
                throw new ArgumentException(
                    $"Document '{document.Number}' is from another supplier; a payment settles one supplier only.",
                    nameof(request));
            }

            if (document.IsVoided)
                throw new ArgumentException($"Document '{document.Number}' is voided and cannot be settled.", nameof(request));

            // Filtering the list is not enough: the request carries document ids, so the rule has
            // to hold here too.
            if (!IsOpenInCurrentAccount(document.DocumentType))
            {
                throw new ArgumentException(
                    $"Document '{document.Number}' is a '{document.DocumentType}' and leaves nothing owed, so a payment cannot settle it.",
                    nameof(request));
            }

            // For a credit note the same arithmetic reads as credit left: what it gave, less what
            // earlier payments already took off.
            var alreadyUsed = paid.TryGetValue(document.Id, out var amount) ? amount : 0m;
            var outstanding = FiscalRounding.Amount(document.GrossTotal - alreadyUsed);

            if (line.AppliedAmount > outstanding)
            {
                throw new ArgumentException(
                    PurchaseDocumentTypes.IsCredit(document.DocumentType)
                        ? $"Credit note '{document.Number}' has {outstanding:0.00} of credit left but {line.AppliedAmount:0.00} was used."
                        : $"Document '{document.Number}' owes {outstanding:0.00} but {line.AppliedAmount:0.00} was applied to it.",
                    nameof(request));
            }

            lines.Add(new SupplierPaymentLine
            {
                DocumentKind = document.Kind,
                DocumentId = document.Id,
                DocumentType = document.DocumentType,
                DocumentNumber = document.Number,
                DocumentDate = document.Date,
                AppliedAmount = line.AppliedAmount
            });

            lineNumber++;
        }

        return lines;
    }

    /// <summary>Reads the document from whichever table the line says it lives in.</summary>
    private async Task<PayableDocument> ResolveDocumentAsync(
        SupplierPaymentLineRequest line,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PayableDocumentKind>(line.DocumentKind, ignoreCase: true, out var kind)
            || !Enum.IsDefined(kind))
        {
            throw new ArgumentException($"Unknown document kind '{line.DocumentKind}'.", nameof(line));
        }

        if (kind == PayableDocumentKind.PurchaseInvoice)
        {
            var invoice = await invoiceStorage.GetByIdAsync(line.DocumentId, cancellationToken)
                ?? throw new ArgumentException($"Supplier invoice '{line.DocumentId}' was not found.", nameof(line));

            return new PayableDocument(
                kind,
                invoice.Id,
                invoice.CompanyId,
                invoice.SupplierId,
                invoice.DocumentType,
                invoice.SupplierDocumentNumber,
                invoice.SupplierDocumentDate,
                invoice.GrossTotal,
                invoice.IsVoided);
        }

        var selfBilled = await selfBilledStorage.GetByIdAsync(line.DocumentId, cancellationToken)
            ?? throw new ArgumentException($"Self-billed invoice '{line.DocumentId}' was not found.", nameof(line));

        return new PayableDocument(
            kind,
            selfBilled.Id,
            selfBilled.CompanyId,
            selfBilled.SupplierId,
            selfBilled.DocumentType,
            selfBilled.DocumentNumber,
            selfBilled.IssueDate,
            selfBilled.GrossTotal,
            selfBilled.IsVoided);
    }

    private static List<SupplierPaymentMethod> BuildMethods(IReadOnlyList<SupplierPaymentMethodRequest> requestMethods)
    {
        var methods = new List<SupplierPaymentMethod>(requestMethods.Count);

        foreach (var method in requestMethods)
        {
            if (!PaymentMechanisms.IsSupported(method.Mechanism))
                throw new ArgumentException($"Unknown payment mechanism '{method.Mechanism}'.", nameof(requestMethods));

            methods.Add(new SupplierPaymentMethod
            {
                Mechanism = method.Mechanism,
                Amount = method.Amount,
                PaymentDate = method.PaymentDate
            });
        }

        return methods;
    }

    /// <summary>
    /// What a payment can take: documents that leave money owed, and credit notes, whose credit it
    /// takes off. A fatura-recibo was paid when issued, so it never is. The types are the same for a
    /// supplier's invoice and for one we self-billed.
    /// </summary>
    private static bool IsOpenInCurrentAccount(string documentType) =>
        PurchaseDocumentTypes.IsSettledByPayment(documentType) || PurchaseDocumentTypes.IsCredit(documentType);

    private static SupplierSnapshot ToSnapshot(PurchaseOrderSupplierDto supplier) =>
        new(supplier.Code,
            supplier.Name,
            supplier.TaxId,
            supplier.Address,
            supplier.PostalCode,
            supplier.City,
            supplier.Country);

    private static SupplierPaymentListItemDto MapListItem(SupplierPayment payment) =>
        new()
        {
            Id = payment.Id,
            Number = payment.Number,
            PaymentDate = payment.PaymentDate,
            SupplierId = payment.SupplierId,
            SupplierName = payment.Supplier.Name,
            SupplierTaxId = payment.Supplier.TaxId,
            Total = payment.Total,
            SettledDocumentCount = payment.Lines.Count,
            Status = payment.Status.ToString(),
            IsVoided = payment.IsVoided
        };

    private static SupplierPaymentDto Map(SupplierPayment payment) =>
        new(payment.Id,
            payment.CompanyId,
            payment.SupplierId,
            payment.Number,
            payment.PaymentDate,
            payment.Status.ToString(),
            payment.IsVoided,
            new PurchaseOrderSupplierDto(
                payment.Supplier.Code,
                payment.Supplier.Name,
                payment.Supplier.TaxId,
                payment.Supplier.Address,
                payment.Supplier.PostalCode,
                payment.Supplier.City,
                payment.Supplier.Country),
            payment.Description,
            payment.Total,
            payment.CreatedAtUtc,
            payment.VoidedAtUtc,
            payment.VoidReason,
            [
                .. payment.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line => new SupplierPaymentLineDto(
                        line.LineNumber,
                        line.DocumentKind.ToString(),
                        line.DocumentId,
                        line.DocumentType,
                        line.DocumentNumber,
                        line.DocumentDate,
                        line.IsCredit,
                        line.AppliedAmount))
            ],
            [
                .. payment.Methods
                    .Select(method => new SupplierPaymentMethodDto(method.Mechanism, method.Amount, method.PaymentDate))
            ]);

    /// <summary>The two kinds of payable document, reduced to what a payment needs to know.</summary>
    private sealed record PayableDocument(
        PayableDocumentKind Kind,
        Guid Id,
        Guid CompanyId,
        Guid SupplierId,
        string DocumentType,
        string Number,
        DateOnly Date,
        decimal GrossTotal,
        bool IsVoided);
}
