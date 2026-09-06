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
    ISalesUnitOfWork unitOfWork,
    IDocumentSigner signer,
    IOptions<FiscalOptions> fiscalOptions) : ISalesDocumentService
{
    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<IReadOnlyList<InvoiceListItemDto>> GetAllAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var documents = await documentStorage.GetAllAsync(companyId, cancellationToken);

        return documents
            .Select(x => new InvoiceListItemDto(
                x.Id,
                x.DocumentNumber,
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
        return document is null ? null : Map(document);
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

        if (!series.CanIssue)
            throw new InvalidOperationException(
                $"Series '{series.SeriesCode}' has no validation code from the tax authority yet, so it cannot issue documents.");

        var lines = BuildLines(request.Lines);
        var taxSummaries = BuildTaxSummaries(lines);

        var netTotal = FiscalRounding.Amount(lines.Sum(x => x.LineAmount));
        var taxPayable = FiscalRounding.Amount(lines.Sum(x => x.TaxAmount));
        var grossTotal = FiscalRounding.Amount(netTotal + taxPayable);

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
            userId);

        document.AttachQrCodePayload(BuildQrCodePayload(document, signature.Hash));

        await documentStorage.AddAsync(document, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(document);
    }

    public async Task<InvoiceDetailDto?> VoidAsync(Guid id, string reason, string? userId, CancellationToken cancellationToken = default)
    {
        var document = await documentStorage.GetByIdAsync(id, cancellationToken);
        if (document is null)
            return null;

        var statusChange = document.Void(reason, userId, DateTime.UtcNow);

        await documentStorage.AddStatusChangeAsync(statusChange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(document);
    }

    private static List<SalesDocumentLine> BuildLines(IReadOnlyList<CreateInvoiceLineRequest> requestLines)
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
                TaxExemptionReason = line.TaxExemptionReason
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
            HashCharacters = RsaDocumentSigner.ExtractQrCodeHash(hash),
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

    private static InvoiceDetailDto Map(SalesDocument document) =>
        new(document.Id,
            document.DocumentNumber,
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
                    x.TaxAmount))
                .ToList(),
            document.TaxSummaries
                .Select(x => new InvoiceTaxDto(
                    x.TaxCountryRegion,
                    x.TaxCode,
                    x.TaxPercentage,
                    x.TaxableBase,
                    x.TaxAmount))
                .ToList());
}
