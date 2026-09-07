using Erp.FiscalPT;
using Erp.FiscalPT.Documents;
using Erp.FiscalPT.QrCode;
using Erp.FiscalPT.Signing;
using Erp.Sales.Application.Configuration;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Erp.Sales.Application.Services;

public sealed class SalesDocumentService(
    ISalesDocumentStorage documentStorage,
    ISeriesStorage seriesStorage,
    IStockMovementStorage movementStorage,
    ISalesUnitOfWork unitOfWork,
    IDocumentSigner signer,
    IOptions<FiscalOptions> fiscalOptions) : ISalesDocumentService
{
    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<IReadOnlyList<PendingMovementLineDto>> GetPendingMovementLinesAsync(
        Guid companyId,
        string? partyTaxId = null,
        CancellationToken cancellationToken = default)
    {
        var movements = await movementStorage.GetInvoiceableAsync(companyId, cancellationToken);

        var candidates = movements
            .Where(movement => !movement.IsVoided)
            .Where(movement => MovementDocumentTypes.IsInvoiceable(movement.MovementType))
            .Where(movement => string.IsNullOrWhiteSpace(partyTaxId)
                               || string.Equals(movement.PartyTaxId, partyTaxId, StringComparison.Ordinal))
            .ToList();

        var lineIds = candidates.SelectMany(movement => movement.Lines).Select(line => line.Id).ToList();
        var invoiced = await documentStorage.GetInvoicedQuantitiesAsync(lineIds, cancellationToken);

        return
        [
            .. candidates
                .SelectMany(movement => movement.Lines.OrderBy(line => line.LineNumber),
                    (movement, line) => new { movement, line })
                .Select(entry =>
                {
                    var already = invoiced.TryGetValue(entry.line.Id, out var quantity) ? quantity : 0m;

                    return new PendingMovementLineDto(
                        entry.movement.Id,
                        entry.line.Id,
                        entry.movement.DocumentNumber,
                        entry.movement.MovementType,
                        entry.movement.MovementDate,
                        entry.movement.PartyName,
                        entry.movement.PartyTaxId,
                        entry.line.LineNumber,
                        entry.line.ProductCode,
                        entry.line.ProductDescription,
                        entry.line.UnitOfMeasure,
                        entry.line.Quantity,
                        already,
                        entry.line.Quantity - already,
                        entry.line.UnitPrice,
                        entry.line.TaxCountryRegion,
                        entry.line.TaxCode,
                        entry.line.TaxPercentage,
                        entry.line.TaxExemptionCode,
                        entry.line.TaxExemptionReason);
                })
                .Where(line => line.PendingQuantity > 0)
        ];
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var documents = await documentStorage.GetAllAsync(companyId, cancellationToken);

        return documents
            .Select(x => new InvoiceListItemDto(
                x.Id,
                x.DocumentNumber,
                x.DocumentType,
                x.Atcud,
                x.DocumentDate,
                x.CustomerName,
                x.CustomerTaxId,
                x.NetTotal,
                x.TaxPayable,
                x.GrossTotal,
                x.EffectiveStatus))
            .ToList();
    }

    public async Task<InvoiceDetailDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var document = await documentStorage.GetByIdAsync(id, cancellationToken);

        if (document is null)
            return null;

        // What is left to credit is what the credit note screen needs to know before it starts.
        return Map(document, await documentStorage.GetCreditedAmountAsync(id, cancellationToken));
    }

    public async Task<InvoiceDetailDto> IssueAsync(CreateInvoiceRequest request, string? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Lines is null || request.Lines.Count == 0)
            throw new ArgumentException("A document must have at least one line.", nameof(request));

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // The series row is locked for the whole transaction: without it, two concurrent
        // issues could take the same sequence number or chain onto the same previous hash.
        var series = await seriesStorage.GetForUpdateAsync(request.SeriesId, cancellationToken)
            ?? throw new ArgumentException($"Series '{request.SeriesId}' was not found.", nameof(request));

        if (series.CompanyId != request.CompanyId)
            throw new ArgumentException("The series does not belong to the requested company.", nameof(request));

        // Without this an invoice could be numbered from a receipt or transport series, burning
        // numbers in a series that belongs to another document family.
        if (!SalesDocumentTypes.IsSupported(series.DocumentType))
            throw new ArgumentException(
                $"Series '{series.SeriesCode}' is for '{series.DocumentType}' documents, not for invoicing.",
                nameof(request));

        if (!series.CanIssue)
            throw new InvalidOperationException(
                $"Series '{series.SeriesCode}' has no validation code from the tax authority yet, so it cannot issue documents.");

        var rectified = await ResolveRectifiedDocumentAsync(request, series.DocumentType, cancellationToken);

        // Locks the delivery notes being invoiced, so two invoices cannot consume the same
        // quantity at the same time, each blind to the other.
        var origins = await ResolveMovementOriginsAsync(request, cancellationToken);

        var lines = BuildLines(request.Lines, origins);
        var taxSummaries = BuildTaxSummaries(lines);

        var netTotal = FiscalRounding.Amount(lines.Sum(x => x.LineAmount));
        var taxPayable = FiscalRounding.Amount(lines.Sum(x => x.TaxAmount));
        var grossTotal = FiscalRounding.Amount(netTotal + taxPayable);

        await EnsureCreditFitsAsync(series.DocumentType, rectified, grossTotal, cancellationToken);

        var rectifies = rectified is null
            ? null
            : new RectifiedDocument(rectified.Id, rectified.DocumentNumber, request.RectificationReason!.Trim());

        var sequenceNumber = series.TakeNextSequence();
        var documentNumber = DocumentNumber.Build(series.DocumentType, series.SeriesCode, sequenceNumber);
        var atcud = Atcud.Build(series.ValidationCode!, sequenceNumber);

        // Seconds precision, because that is what goes into the signed string.
        var systemEntryDateUtc = TruncateToSeconds(DateTime.UtcNow);
        var previousHash = await documentStorage.GetLastHashAsync(series.Id, cancellationToken);

        var signature = signer.Sign(request.DocumentDate, systemEntryDateUtc, documentNumber, grossTotal, previousHash);

        var customer = ToSnapshot(request.Customer);

        var document = SalesDocument.Issue(
            request.CompanyId,
            series,
            sequenceNumber,
            documentNumber,
            atcud,
            request.DocumentDate,
            systemEntryDateUtc,
            customer,
            lines,
            taxSummaries,
            netTotal,
            taxPayable,
            grossTotal,
            signature.Hash,
            previousHash,
            signature.HashControl,
            userId,
            rectifies);

        document.AttachQrCodePayload(BuildQrCodePayload(document, signature.Hash));

        await documentStorage.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // A document that has just been issued cannot have been credited yet.
        return Map(document, 0m);
    }

    public async Task<InvoiceDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        var document = await documentStorage.GetByIdAsync(id, cancellationToken);
        if (document is null)
            return null;

        var statusChange = document.Void(reason, userId, DateTime.UtcNow);

        await documentStorage.AddStatusChangeAsync(statusChange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(document, await documentStorage.GetCreditedAmountAsync(id, cancellationToken));
    }

    /// <summary>
    /// Resolves the document a credit or debit note corrects. Article 36.º n.º 5 of the CIVA makes
    /// the reference mandatory on a rectifying document, and meaningless on any other, so both
    /// directions are enforced here.
    /// </summary>
    /// <summary>
    /// A document cannot be credited for more than it is worth. Credit notes already issued against
    /// it count towards that ceiling; voided ones do not, because they credit nothing.
    /// </summary>
    /// <remarks>
    /// Only credit notes are capped. A debit note adds to what the customer owes, so it takes
    /// nothing away from the original document.
    /// </remarks>
    private async Task EnsureCreditFitsAsync(
        string documentType,
        SalesDocument? rectified,
        decimal grossTotal,
        CancellationToken cancellationToken)
    {
        if (rectified is null || !string.Equals(documentType, SalesDocumentTypes.CreditNote, StringComparison.Ordinal))
            return;

        var alreadyCredited = await documentStorage.GetCreditedAmountAsync(rectified.Id, cancellationToken);
        var creditable = FiscalRounding.Amount(rectified.GrossTotal - alreadyCredited);

        if (grossTotal <= creditable)
            return;

        throw new ArgumentException(
            alreadyCredited > 0
                ? $"Document '{rectified.DocumentNumber}' is worth {rectified.GrossTotal:0.00}, of which {alreadyCredited:0.00} is already credited: only {creditable:0.00} can still be credited, not {grossTotal:0.00}."
                : $"Document '{rectified.DocumentNumber}' is worth {creditable:0.00}, so it cannot be credited for {grossTotal:0.00}.",
            nameof(grossTotal));
    }

    private async Task<SalesDocument?> ResolveRectifiedDocumentAsync(
        CreateInvoiceRequest request,
        string documentType,
        CancellationToken cancellationToken)
    {
        if (!SalesDocumentTypes.IsRectifying(documentType))
        {
            if (request.RectifiedDocumentId is not null)
            {
                throw new ArgumentException(
                    $"A '{documentType}' does not correct another document, so it cannot reference one.",
                    nameof(request));
            }

            return null;
        }

        if (request.RectifiedDocumentId is not { } rectifiedId || rectifiedId == Guid.Empty)
        {
            throw new ArgumentException(
                $"A '{documentType}' must identify the document it corrects.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.RectificationReason))
        {
            throw new ArgumentException(
                $"A '{documentType}' must state why the document is being corrected.",
                nameof(request));
        }

        // Locked, not merely read: the row is the mutex that stops two credit notes from being
        // issued against the same document at once, each blind to the other's credit.
        var rectified = await documentStorage.GetForUpdateAsync(rectifiedId, cancellationToken)
            ?? throw new ArgumentException($"Document '{rectifiedId}' was not found.", nameof(request));

        if (rectified.CompanyId != request.CompanyId)
            throw new ArgumentException("The corrected document belongs to another company.", nameof(request));

        if (rectified.IsVoided)
        {
            throw new ArgumentException(
                $"Document '{rectified.DocumentNumber}' is voided, so there is nothing to correct.",
                nameof(request));
        }

        if (SalesDocumentTypes.IsRectifying(rectified.DocumentType))
        {
            throw new ArgumentException(
                $"Document '{rectified.DocumentNumber}' is itself a rectifying document.",
                nameof(request));
        }

        return rectified;
    }

    /// <summary>
    /// Checks the delivery note lines an invoice is built from, and returns what each one still has
    /// left to invoice. The movements are locked first, so the quantities read here cannot be taken
    /// by another invoice while this one is being issued.
    /// </summary>
    private async Task<Dictionary<Guid, MovementOrigin>> ResolveMovementOriginsAsync(
        CreateInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        var lineIds = request.Lines
            .Where(line => line.OriginatingLineId is not null)
            .Select(line => line.OriginatingLineId!.Value)
            .Distinct()
            .ToList();

        if (lineIds.Count == 0)
            return [];

        if (lineIds.Count != request.Lines.Count(line => line.OriginatingLineId is not null))
            throw new ArgumentException("The same delivery note line cannot be invoiced twice on one document.", nameof(request));

        var movements = await movementStorage.GetForUpdateByLinesAsync(lineIds, cancellationToken);
        var invoiced = await documentStorage.GetInvoicedQuantitiesAsync(lineIds, cancellationToken);

        var origins = new Dictionary<Guid, MovementOrigin>(lineIds.Count);

        foreach (var lineId in lineIds)
        {
            var movement = movements.FirstOrDefault(x => x.Lines.Any(line => line.Id == lineId))
                ?? throw new ArgumentException($"Delivery note line '{lineId}' was not found.", nameof(request));

            if (movement.CompanyId != request.CompanyId)
                throw new ArgumentException("The delivery note belongs to another company.", nameof(request));

            if (movement.IsVoided)
            {
                throw new ArgumentException(
                    $"Document '{movement.DocumentNumber}' is voided, so it cannot be invoiced.",
                    nameof(request));
            }

            if (!MovementDocumentTypes.IsInvoiceable(movement.MovementType))
            {
                throw new ArgumentException(
                    $"Document '{movement.DocumentNumber}' is a '{movement.MovementType}' and is never invoiced.",
                    nameof(request));
            }

            var line = movement.Lines.First(x => x.Id == lineId);
            var already = invoiced.TryGetValue(lineId, out var quantity) ? quantity : 0m;

            origins[lineId] = new MovementOrigin(
                movement.DocumentNumber,
                movement.MovementDate,
                line.Quantity - already);
        }

        return origins;
    }

    /// <summary>A delivery note line an invoice is drawing from, and what is left of it.</summary>
    private sealed record MovementOrigin(string DocumentNumber, DateOnly MovementDate, decimal PendingQuantity);

    private static List<SalesDocumentLine> BuildLines(
        IReadOnlyList<CreateInvoiceLineRequest> requestLines,
        Dictionary<Guid, MovementOrigin> origins)
    {
        var lines = new List<SalesDocumentLine>(requestLines.Count);
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

            MovementOrigin? origin = null;

            if (line.OriginatingLineId is { } originatingLineId)
            {
                origin = origins[originatingLineId];

                if (line.Quantity > origin.PendingQuantity)
                {
                    throw new ArgumentException(
                        $"Line {lineNumber} invoices {line.Quantity:0.######} of '{origin.DocumentNumber}', which only has {origin.PendingQuantity:0.######} left to invoice.",
                        nameof(requestLines));
                }
            }

            var lineAmount = FiscalRounding.Amount(line.Quantity * line.UnitPrice);
            var taxAmount = FiscalRounding.Amount(lineAmount * line.TaxPercentage / 100m);

            lines.Add(new SalesDocumentLine
            {
                LineNumber = lineNumber,
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
                TaxExemptionReason = line.TaxExemptionReason,
                OriginatingLineId = line.OriginatingLineId,
                OriginatingNumber = origin?.DocumentNumber,
                OriginatingDate = origin?.MovementDate
            });

            lineNumber++;
        }

        return lines;
    }

    private static List<DocumentTaxSummary> BuildTaxSummaries(IReadOnlyList<SalesDocumentLine> lines) =>
        lines
            .GroupBy(x => new { x.TaxCountryRegion, x.TaxCode, x.TaxPercentage })
            .Select(group => new DocumentTaxSummary
            {
                TaxCountryRegion = group.Key.TaxCountryRegion,
                TaxCode = group.Key.TaxCode,
                TaxPercentage = group.Key.TaxPercentage,
                TaxableBase = FiscalRounding.Amount(group.Sum(x => x.LineAmount)),
                TaxAmount = FiscalRounding.Amount(group.Sum(x => x.TaxAmount))
            })
            .ToList();

    private string BuildQrCodePayload(SalesDocument document, string hash)
    {
        var fields = new QrCodeFields
        {
            IssuerTaxId = _fiscal.IssuerTaxId,
            BuyerTaxId = document.CustomerTaxId,
            BuyerCountry = document.CustomerCountry,
            DocumentType = document.DocumentType,
            DocumentStatus = document.EffectiveStatus,
            DocumentDate = document.DocumentDate,
            DocumentNumber = document.DocumentNumber,
            Atcud = document.Atcud,
            TaxAmounts = document.TaxSummaries
                .Select(x => new QrCodeTaxAmount(x.TaxCountryRegion, x.TaxCode, x.TaxableBase, x.TaxAmount))
                .ToList(),
            TotalTaxes = document.TaxPayable,
            GrossTotal = document.GrossTotal,
            HashCharacters = RsaDocumentSigner.ExtractPrintableHash(hash),
            CertificateNumber = _fiscal.CertificateNumber
        };

        return QrCodePayloadBuilder.Build(fields);
    }

    private static CustomerSnapshot ToSnapshot(CustomerRequest customer)
    {
        if (string.IsNullOrWhiteSpace(customer.TaxId))
            return CustomerSnapshot.FinalConsumer() with { Name = customer.Name, Address = customer.Address };

        return new CustomerSnapshot(customer.TaxId.Trim(), customer.Name, customer.Address, customer.Country);
    }

    private static DateTime TruncateToSeconds(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, DateTimeKind.Utc);

    private static InvoiceDetailDto Map(SalesDocument document, decimal creditedAmount) =>
        new(document.Id,
            document.DocumentNumber,
            document.DocumentType,
            document.Atcud,
            document.DocumentDate,
            document.SystemEntryDateUtc,
            document.EffectiveStatus,
            document.CustomerName,
            document.CustomerTaxId,
            document.CustomerAddress,
            document.NetTotal,
            document.TaxPayable,
            document.GrossTotal,
            RsaDocumentSigner.ExtractPrintableHash(document.Hash),
            document.QrCodePayload,
            document.Lines
                .OrderBy(x => x.LineNumber)
                .Select(x => new InvoiceLineDto(
                    x.LineNumber,
                    x.ProductCode,
                    x.ProductDescription,
                    x.Quantity,
                    x.UnitOfMeasure,
                    x.UnitPrice,
                    x.LineAmount,
                    x.TaxCode,
                    x.TaxPercentage,
                    x.TaxAmount,
                    x.TaxExemptionCode,
                    x.TaxExemptionReason,
                    x.OriginatingNumber,
                    x.OriginatingDate))
                .ToList(),
            document.TaxSummaries
                .Select(x => new InvoiceTaxDto(
                    x.TaxCountryRegion,
                    x.TaxCode,
                    x.TaxPercentage,
                    x.TaxableBase,
                    x.TaxAmount))
                .ToList(),
            document.RectifiedDocumentNumber,
            document.RectificationReason,
            creditedAmount);
}
