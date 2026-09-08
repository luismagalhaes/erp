using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.QrCode;
using Erp.FiscalPT.Signing;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Erp.Common;
using Erp.SeriesRegistry.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

/// <summary>
/// Issues receipts. A receipt is a fiscally relevant document �?" numbered, signed and immutable
/// like an invoice �?" and it may never settle more than an invoice actually owes.
/// </summary>
public sealed class PaymentService(
    IPaymentStorage paymentStorage,
    ISalesDocumentStorage documentStorage,
    ISeriesStorage seriesStorage,
    IUnitOfWork unitOfWork,
    IDocumentSigner signer,
    IOptions<FiscalOptions> fiscalOptions) : IPaymentService
{
    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<IReadOnlyList<PaymentListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var payments = await paymentStorage.GetAllAsync(companyId, cancellationToken);

        return payments
            .Select(x => new PaymentListItemDto(
                x.Id,
                x.PaymentRefNo,
                x.PaymentType,
                x.Atcud,
                x.TransactionDate,
                x.PartyName,
                x.PartyTaxId,
                x.GrossTotal,
                x.Lines.Count,
                x.EffectiveStatus))
            .ToList();
    }

    public async Task<PaymentDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await paymentStorage.GetByIdAsync(id, cancellationToken);
        return payment is null ? null : Map(payment);
    }

    public async Task<IReadOnlyList<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(
        Guid companyId,
        string? customerTaxId = null,
        CancellationToken cancellationToken = default)
    {
        var documents = await documentStorage.GetAllAsync(companyId, cancellationToken);

        var candidates = documents
            .Where(document => !document.IsVoided)
            // A fatura-recibo carries its own receipt and a nota de crédito lowers the debt, so
            // neither is ever waiting to be settled.
            .Where(document => SalesDocumentTypes.IsSettledByReceipt(document.DocumentType))
            .Where(document => string.IsNullOrWhiteSpace(customerTaxId)
                               || string.Equals(document.CustomerTaxId, customerTaxId, StringComparison.Ordinal))
            .ToList();

        var settled = await paymentStorage.GetSettledAmountsAsync(
            candidates.Select(document => document.Id).ToList(), cancellationToken);

        return candidates
            .Select(document =>
            {
                var alreadySettled = settled.TryGetValue(document.Id, out var amount) ? amount : 0m;
                return new OutstandingInvoiceDto(
                    document.Id,
                    document.DocumentNumber,
                    document.DocumentDate,
                    document.CustomerName,
                    document.CustomerTaxId,
                    document.GrossTotal,
                    alreadySettled,
                    FiscalRounding.Amount(document.GrossTotal - alreadySettled));
            })
            .Where(invoice => invoice.OutstandingAmount > 0)
            .OrderBy(invoice => invoice.DocumentDate)
            .ToList();
    }

    public async Task<PaymentDetailDto> IssueAsync(
        CreatePaymentRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.PartyName);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("A receipt must settle at least one invoice.", nameof(request));

        if (request.Methods is null || request.Methods.Count == 0)
            throw new ArgumentException("A receipt must record how the money was received.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The series row is locked for the whole transaction: without it, two concurrent
        // issues could take the same sequence number or chain onto the same previous hash.
        var series = await seriesStorage.GetForUpdateAsync(request.SeriesId, cancellationToken)
            ?? throw new ArgumentException($"Series '{request.SeriesId}' was not found.", nameof(request));

        if (series.CompanyId != request.CompanyId)
            throw new ArgumentException("The series does not belong to the requested company.", nameof(request));

        if (!PaymentDocumentTypes.IsSupported(series.DocumentType))
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' is for '{series.DocumentType}' documents, not for receipts.",
                nameof(request));

        if (!series.CanIssue)
            throw new InvalidOperationException(
                $"Series '{series.SeriesCode}' has no validation code from the tax authority yet, so it cannot issue documents.");

        var lines = await BuildLinesAsync(request, cancellationToken);
        var methods = BuildMethods(request.Methods);

        var grossTotal = FiscalRounding.Amount(lines.Sum(line => line.AppliedAmount));
        var methodsTotal = FiscalRounding.Amount(methods.Sum(method => method.Amount));

        if (methodsTotal != grossTotal)
        {
            throw new ArgumentException(
                $"The payment methods add up to {methodsTotal:0.00} but the receipt settles {grossTotal:0.00}.",
                nameof(request));
        }

        var sequenceNumber = series.TakeNextSequence();
        var paymentRefNo = DocumentNumber.Build(series.DocumentType, series.SeriesCode, sequenceNumber);
        var atcud = Atcud.Build(series.ValidationCode!, sequenceNumber);

        // Seconds precision, because that is what goes into the signed string.
        var systemEntryDateUtc = TruncateToSeconds(DateTime.UtcNow);
        var previousHash = await paymentStorage.GetLastHashAsync(series.Id, cancellationToken);

        var signature = signer.Sign(request.TransactionDate, systemEntryDateUtc, paymentRefNo, grossTotal, previousHash);

        var party = new PaymentParty(
            string.IsNullOrWhiteSpace(request.PartyTaxId) ? CustomerSnapshot.FinalConsumerTaxId : request.PartyTaxId.Trim(),
            request.PartyName.Trim());

        var payment = Payment.Issue(
            request.CompanyId,
            series,
            sequenceNumber,
            paymentRefNo,
            atcud,
            request.TransactionDate,
            systemEntryDateUtc,
            party,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            lines,
            methods,
            grossTotal,
            signature.Hash,
            previousHash,
            signature.HashControl,
            userId);

        payment.AttachQrCodePayload(BuildQrCodePayload(payment, signature.Hash));

        await paymentStorage.AddAsync(payment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(payment);
    }

    public async Task<PaymentDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        var payment = await paymentStorage.GetByIdAsync(id, cancellationToken);
        if (payment is null)
            return null;

        var statusChange = payment.Void(reason, userId, DateTime.UtcNow);

        await paymentStorage.AddStatusChangeAsync(statusChange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(payment);
    }

    /// <summary>
    /// Turns the requested settlements into lines, checking that each invoice exists, belongs to
    /// the company, is not voided, and still owes at least what is being applied to it.
    /// </summary>
    private async Task<List<PaymentLine>> BuildLinesAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        var lines = new List<PaymentLine>(request.Lines.Count);
        var lineNumber = 1;

        var documentIds = request.Lines.Select(line => line.OriginatingDocumentId).Distinct().ToList();

        if (documentIds.Count != request.Lines.Count)
            throw new ArgumentException("The same invoice cannot be settled twice on one receipt.", nameof(request));

        var settled = await paymentStorage.GetSettledAmountsAsync(documentIds, cancellationToken);

        foreach (var line in request.Lines)
        {
            if (line.AppliedAmount <= 0)
                throw new ArgumentException($"Line {lineNumber} must settle a positive amount.", nameof(request));

            var document = await documentStorage.GetByIdAsync(line.OriginatingDocumentId, cancellationToken)
                ?? throw new ArgumentException($"Invoice '{line.OriginatingDocumentId}' was not found.", nameof(request));

            if (document.CompanyId != request.CompanyId)
                throw new ArgumentException("The invoice does not belong to the requested company.", nameof(request));

            if (document.IsVoided)
                throw new ArgumentException($"Invoice '{document.DocumentNumber}' is voided and cannot be settled.", nameof(request));

            // Filtering the list is not enough: the request carries document ids, so the rule has
            // to hold here too.
            if (!SalesDocumentTypes.IsSettledByReceipt(document.DocumentType))
            {
                throw new ArgumentException(
                    $"Document '{document.DocumentNumber}' is a '{document.DocumentType}' and leaves nothing owed, so a receipt cannot settle it.",
                    nameof(request));
            }

            var alreadySettled = settled.TryGetValue(document.Id, out var amount) ? amount : 0m;
            var outstanding = FiscalRounding.Amount(document.GrossTotal - alreadySettled);

            if (line.AppliedAmount > outstanding)
            {
                throw new ArgumentException(
                    $"Invoice '{document.DocumentNumber}' owes {outstanding:0.00} but {line.AppliedAmount:0.00} was applied to it.",
                    nameof(request));
            }

            lines.Add(new PaymentLine
            {
                LineNumber = lineNumber,
                OriginatingDocumentId = document.Id,
                OriginatingNumber = document.DocumentNumber,
                OriginatingDate = document.DocumentDate,
                AppliedAmount = FiscalRounding.Amount(line.AppliedAmount)
            });

            lineNumber++;
        }

        return lines;
    }

    private static List<PaymentMethodEntry> BuildMethods(IReadOnlyList<CreatePaymentMethodRequest> requestMethods)
    {
        var methods = new List<PaymentMethodEntry>(requestMethods.Count);

        foreach (var method in requestMethods)
        {
            if (!PaymentMechanisms.IsSupported(method.Mechanism))
                throw new ArgumentException($"Unknown payment mechanism '{method.Mechanism}'.", nameof(requestMethods));

            if (method.Amount <= 0)
                throw new ArgumentException("A payment method must carry a positive amount.", nameof(requestMethods));

            methods.Add(new PaymentMethodEntry
            {
                Mechanism = method.Mechanism,
                Amount = FiscalRounding.Amount(method.Amount),
                PaymentDate = method.PaymentDate
            });
        }

        return methods;
    }

    private string BuildQrCodePayload(Payment payment, string hash)
    {
        var fields = new QrCodeFields
        {
            IssuerTaxId = _fiscal.IssuerTaxId,
            BuyerTaxId = payment.PartyTaxId,
            DocumentType = payment.PaymentType,
            DocumentStatus = payment.EffectiveStatus,
            DocumentDate = payment.TransactionDate,
            DocumentNumber = payment.PaymentRefNo,
            Atcud = payment.Atcud,
            TotalTaxes = payment.TaxPayable,
            GrossTotal = payment.GrossTotal,
            HashCharacters = RsaDocumentSigner.ExtractPrintableHash(hash),
            CertificateNumber = _fiscal.CertificateNumber
        };

        return QrCodePayloadBuilder.Build(fields);
    }

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);

    private static PaymentDetailDto Map(Payment payment) =>
        new(payment.Id,
            payment.PaymentRefNo,
            payment.PaymentType,
            payment.Atcud,
            payment.TransactionDate,
            payment.SystemEntryDateUtc,
            payment.EffectiveStatus,
            payment.PartyName,
            payment.PartyTaxId,
            payment.Description,
            payment.NetTotal,
            payment.TaxPayable,
            payment.GrossTotal,
            RsaDocumentSigner.ExtractPrintableHash(payment.Hash),
            payment.QrCodePayload,
            payment.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new PaymentLineDto(
                    line.LineNumber,
                    line.OriginatingDocumentId,
                    line.OriginatingNumber,
                    line.OriginatingDate,
                    line.AppliedAmount))
                .ToList(),
            payment.Methods
                .Select(method => new PaymentMethodDto(method.Mechanism, method.Amount, method.PaymentDate))
                .ToList());
}
