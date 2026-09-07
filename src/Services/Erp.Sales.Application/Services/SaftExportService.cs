using Erp.FiscalPT.Documents;
using Erp.FiscalPT.Saft;
using Erp.Sales.Application.Configuration;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Application;
using Erp.Sales.Infrastructure.Contracts;
using Erp.Sales.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace Erp.Sales.Application.Services;

/// <summary>
/// Builds the SAF-T (PT) file for a period. The master files are derived from the documents
/// themselves — every document carries a snapshot of its customer, its products and its tax
/// rates — so the file is internally consistent even if the master data changed afterwards,
/// which is exactly what the tax authority is checking for.
/// </summary>
public sealed class SaftExportService(
    ISalesDocumentStorage documentStorage,
    IStockMovementStorage movementStorage,
    IPaymentStorage paymentStorage,
    IOptions<FiscalOptions> fiscalOptions) : ISaftExportService
{
    /// <summary>Name of the program as registered with the tax authority, for the header.</summary>
    private const string ProductId = "ErpPortugal/ErpPortugal";

    private const string ProductVersion = "1.0";

    private readonly FiscalOptions _fiscal = fiscalOptions.Value;

    public async Task<SaftPeriodSummaryDto> GetSummaryAsync(
        Guid companyId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        EnsureValidPeriod(startDate, endDate);

        var documents = await documentStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);
        var movements = await movementStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);
        var payments = await paymentStorage.GetForPeriodAsync(companyId, startDate, endDate, cancellationToken);

        return new SaftPeriodSummaryDto(
            startDate,
            endDate,
            documents.Count,
            movements.Count,
            payments.Count,
            documents.Where(x => !x.IsVoided).Sum(x => x.GrossTotal),
            payments.Where(x => !x.IsVoided).Sum(x => x.GrossTotal));
    }

    public async Task<SaftExportResult> ExportAsync(
        SaftExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Company);
        EnsureValidPeriod(request.StartDate, request.EndDate);

        if (string.IsNullOrWhiteSpace(request.Company.TaxId))
            throw new ArgumentException("The company has no tax id, which the SAF-T header requires.", nameof(request));

        var documents = await documentStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        var movements = await movementStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        var payments = await paymentStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        var file = new SaftAuditFile
        {
            Header = BuildHeader(request),
            Customers = BuildCustomers(documents, movements, payments),
            Products = BuildProducts(documents, movements),
            TaxTable = BuildTaxTable(documents, movements),
            Invoices = [.. documents.Select(MapInvoice)],
            StockMovements = [.. movements.Select(MapStockMovement)],
            Payments = [.. payments.Select(MapPayment)]
        };

        var document = SaftXmlWriter.Build(file);

        // Validated here rather than only in the tests, so a document the writer was never
        // exercised on cannot reach the tax authority unnoticed.
        var validationErrors = SaftSchemaValidator.Validate(document);

        return new SaftExportResult(
            SaftXmlWriter.BuildFileName(file.Header),
            SaftXmlWriter.Serialize(file),
            documents.Count,
            movements.Count,
            payments.Count,
            validationErrors);
    }

    private SaftHeader BuildHeader(SaftExportRequest request)
    {
        var company = request.Company;

        return new SaftHeader
        {
            // With no registry number on file, the tax id stands in, which the schema allows.
            CompanyId = company.TaxId,
            TaxRegistrationNumber = OnlyDigits(company.TaxId),
            CompanyName = string.IsNullOrWhiteSpace(company.LegalName) ? company.Name : company.LegalName,
            BusinessName = company.Name,
            CompanyAddress = new SaftAddress
            {
                AddressDetail = company.Address ?? SaftConstants.Unknown,
                City = company.City ?? SaftConstants.Unknown,
                PostalCode = company.PostalCode,
                Country = company.Country
            },
            FiscalYear = request.StartDate.Year,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            DateCreated = DateOnly.FromDateTime(DateTime.Today),
            ProductCompanyTaxId = OnlyDigits(_fiscal.IssuerTaxId),
            SoftwareCertificateNumber = _fiscal.CertificateNumber,
            ProductId = ProductId,
            ProductVersion = ProductVersion,
            Telephone = company.Phone,
            Email = company.Email
        };
    }

    /// <summary>
    /// One entry per tax id used in the period. The name comes from the most recent document, so
    /// a customer renamed halfway through the period is exported under the name it ended with.
    /// </summary>
    private static List<SaftCustomer> BuildCustomers(
        IReadOnlyList<SalesDocument> documents,
        IReadOnlyList<StockMovement> movements,
        IReadOnlyList<Payment> payments)
    {
        var parties = documents
            .Select(x => (TaxId: x.CustomerTaxId, x.CustomerName, Address: x.CustomerAddress, Date: x.DocumentDate))
            .Concat(movements
                .Where(x => !x.PartyIsSupplier)
                .Select(x => (TaxId: x.PartyTaxId, CustomerName: x.PartyName, Address: (string?)null, Date: x.MovementDate)))
            .Concat(payments
                .Select(x => (TaxId: x.PartyTaxId, CustomerName: x.PartyName, Address: (string?)null, Date: x.TransactionDate)));

        return [.. parties
            .Where(party => !string.IsNullOrWhiteSpace(party.TaxId))
            .GroupBy(party => party.TaxId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(party => party.Date).First())
            .OrderBy(party => party.TaxId, StringComparer.Ordinal)
            .Select(party => new SaftCustomer
            {
                CustomerId = party.TaxId,
                CustomerTaxId = party.TaxId,
                CompanyName = party.CustomerName,
                BillingAddress = new SaftAddress { AddressDetail = party.Address ?? SaftConstants.Unknown }
            })];
    }

    /// <summary>One entry per product code that appears on a line in the period.</summary>
    private static List<SaftProduct> BuildProducts(
        IReadOnlyList<SalesDocument> documents,
        IReadOnlyList<StockMovement> movements)
    {
        var lines = documents
            .SelectMany(document => document.Lines)
            .Select(line => (line.ProductCode, line.ProductDescription))
            .Concat(movements
                .SelectMany(movement => movement.Lines)
                .Select(line => (line.ProductCode, line.ProductDescription)));

        return [.. lines
            .Where(line => !string.IsNullOrWhiteSpace(line.ProductCode))
            .GroupBy(line => line.ProductCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SaftProduct
            {
                ProductCode = group.Key,
                ProductDescription = group.First().ProductDescription,
                ProductNumberCode = group.Key
            })];
    }

    /// <summary>
    /// The rates actually used in the period. Declaring only what the documents reference keeps
    /// the table and the lines in agreement, which is what the validator checks.
    /// </summary>
    private static List<SaftTaxTableEntry> BuildTaxTable(
        IReadOnlyList<SalesDocument> documents,
        IReadOnlyList<StockMovement> movements)
    {
        var rates = documents
            .SelectMany(document => document.Lines)
            .Select(line => (line.TaxCountryRegion, line.TaxCode, line.TaxPercentage))
            .Concat(movements
                .SelectMany(movement => movement.Lines)
                .Select(line => (line.TaxCountryRegion, line.TaxCode, line.TaxPercentage)));

        return [.. rates
            .GroupBy(rate => (rate.TaxCountryRegion, rate.TaxCode, rate.TaxPercentage))
            .Select(group => group.Key)
            .OrderBy(rate => rate.TaxCountryRegion, StringComparer.Ordinal)
            .ThenBy(rate => rate.TaxCode, StringComparer.Ordinal)
            .Select(rate => new SaftTaxTableEntry
            {
                TaxCountryRegion = rate.TaxCountryRegion,
                TaxCode = rate.TaxCode,
                Description = DescribeTaxCode(rate.TaxCode),
                TaxPercentage = rate.TaxPercentage
            })];
    }

    private static SaftInvoice MapInvoice(SalesDocument document)
    {
        return new SaftInvoice
        {
            InvoiceNo = document.DocumentNumber,
            Atcud = document.Atcud,
            DocumentStatus = BuildStatus(
                document.EffectiveStatus,
                document.SystemEntryDateUtc,
                document.SourceBilling,
                document.CreatedByUserId,
                document.StatusChanges.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault()),
            Hash = document.Hash,
            HashControl = document.HashControl,
            Period = document.DocumentDate.Month,
            InvoiceDate = document.DocumentDate,
            InvoiceType = document.DocumentType,
            SourceId = SourceId(document.CreatedByUserId),
            SystemEntryDate = document.SystemEntryDateUtc,
            CustomerId = document.CustomerTaxId,
            Lines = [.. document.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SaftInvoiceLine
                {
                    LineNumber = line.LineNumber,
                    ProductCode = line.ProductCode,
                    ProductDescription = line.ProductDescription,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    UnitPrice = line.UnitPrice,
                    TaxPointDate = document.DocumentDate,
                    // A credit or debit note carries the reference on every line.
                    Reference = document.RectifiedDocumentNumber,
                    ReferenceReason = document.RectificationReason,
                    Description = line.ProductDescription,
                    Amount = line.LineAmount,
                    Tax = new SaftTax
                    {
                        TaxCountryRegion = line.TaxCountryRegion,
                        TaxCode = line.TaxCode,
                        TaxPercentage = line.TaxPercentage
                    },
                    TaxExemptionReason = line.TaxExemptionReason,
                    TaxExemptionCode = line.TaxExemptionCode
                })],
            Totals = new SaftDocumentTotals
            {
                TaxPayable = document.TaxPayable,
                NetTotal = document.NetTotal,
                GrossTotal = document.GrossTotal
            }
        };
    }

    private static SaftStockMovement MapStockMovement(StockMovement movement)
    {
        return new SaftStockMovement
        {
            DocumentNumber = movement.DocumentNumber,
            Atcud = movement.Atcud,
            DocumentStatus = BuildStatus(
                movement.EffectiveStatus,
                movement.SystemEntryDateUtc,
                movement.SourceBilling,
                movement.CreatedByUserId,
                movement.StatusChanges.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault()),
            Hash = movement.Hash,
            HashControl = movement.HashControl,
            Period = movement.MovementDate.Month,
            MovementDate = movement.MovementDate,
            MovementType = movement.MovementType,
            SystemEntryDate = movement.SystemEntryDateUtc,
            PartyId = movement.PartyTaxId,
            PartyIsSupplier = movement.PartyIsSupplier,
            SourceId = SourceId(movement.CreatedByUserId),
            MovementComments = movement.Comments,
            ShipTo = MapShippingPoint(movement.ShipTo),
            ShipFrom = MapShippingPoint(movement.ShipFrom),
            MovementStartTime = movement.MovementStartAtUtc,
            MovementEndTime = movement.MovementEndAtUtc,
            AtDocCodeId = movement.AtDocCodeId,
            Lines = [.. movement.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SaftStockMovementLine
                {
                    LineNumber = line.LineNumber,
                    ProductCode = line.ProductCode,
                    ProductDescription = line.ProductDescription,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    UnitPrice = line.UnitPrice,
                    Description = line.ProductDescription,
                    Amount = line.LineAmount,
                    Tax = new SaftTax
                    {
                        TaxCountryRegion = line.TaxCountryRegion,
                        TaxCode = line.TaxCode,
                        TaxPercentage = line.TaxPercentage
                    },
                    TaxExemptionReason = line.TaxExemptionReason,
                    TaxExemptionCode = line.TaxExemptionCode
                })],
            Totals = new SaftDocumentTotals
            {
                TaxPayable = movement.TaxPayable,
                NetTotal = movement.NetTotal,
                GrossTotal = movement.GrossTotal
            }
        };
    }

    private static SaftPayment MapPayment(Payment payment)
    {
        return new SaftPayment
        {
            PaymentRefNo = payment.PaymentRefNo,
            Atcud = payment.Atcud,
            Period = payment.TransactionDate.Month,
            TransactionDate = payment.TransactionDate,
            PaymentType = payment.PaymentType,
            Description = payment.Description,
            DocumentStatus = BuildStatus(
                payment.EffectiveStatus,
                payment.SystemEntryDateUtc,
                payment.SourcePayment,
                payment.CreatedByUserId,
                payment.StatusChanges.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault()),
            PaymentMethods = [.. payment.Methods.Select(method => new SaftPaymentMethod
            {
                PaymentMechanism = method.Mechanism,
                PaymentAmount = method.Amount,
                PaymentDate = method.PaymentDate
            })],
            SourceId = SourceId(payment.CreatedByUserId),
            SystemEntryDate = payment.SystemEntryDateUtc,
            CustomerId = payment.PartyTaxId,
            Lines = [.. payment.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SaftPaymentLine
                {
                    LineNumber = line.LineNumber,
                    OriginatingOn = line.OriginatingNumber,
                    InvoiceDate = line.OriginatingDate,
                    Amount = line.AppliedAmount
                })],
            Totals = new SaftDocumentTotals
            {
                TaxPayable = payment.TaxPayable,
                NetTotal = payment.NetTotal,
                GrossTotal = payment.GrossTotal
            }
        };
    }

    private static SaftShippingPoint MapShippingPoint(MovementLocation location) =>
        new()
        {
            WarehouseId = location.WarehouseId,
            LocationId = location.LocationId,
            Address = new SaftAddress
            {
                AddressDetail = location.Address,
                City = location.City ?? SaftConstants.Unknown,
                PostalCode = location.PostalCode,
                Country = location.Country
            }
        };

    /// <summary>
    /// A voided document reports the status change — when it happened, who did it and why —
    /// instead of the issuing data, because that is the status the file has to declare.
    /// </summary>
    private static SaftDocumentStatus BuildStatus(
        string effectiveStatus,
        DateTime systemEntryDateUtc,
        string sourceBilling,
        string? createdByUserId,
        IStatusChange? lastChange)
    {
        var voided = string.Equals(effectiveStatus, DocumentStatuses.Voided, StringComparison.Ordinal);

        return new SaftDocumentStatus
        {
            Status = effectiveStatus,
            StatusDate = voided && lastChange is not null ? lastChange.OccurredAtUtc : systemEntryDateUtc,
            Reason = voided ? lastChange?.Reason : null,
            SourceId = SourceId(voided ? lastChange?.UserId ?? createdByUserId : createdByUserId),
            SourceBilling = sourceBilling
        };
    }

    private static void EnsureValidPeriod(DateOnly startDate, DateOnly endDate)
    {
        if (endDate < startDate)
            throw new ArgumentException("The end date cannot be before the start date.", nameof(endDate));
    }

    /// <summary>
    /// Who recorded the document. The schema allows 30 characters and Identity issues 36 character
    /// GUIDs, so a long id is trimmed — the remainder is still unique enough to trace the author.
    /// Recording a short user name at issuing time would read better, but it cannot be done
    /// retroactively for documents already issued.
    /// </summary>
    private static string SourceId(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return SaftConstants.Unknown;

        var trimmed = userId.Trim();

        return trimmed.Length <= SaftConstants.SourceIdMaxLength
            ? trimmed
            : trimmed[..SaftConstants.SourceIdMaxLength];
    }

    private static string OnlyDigits(string value) =>
        new([.. value.Where(char.IsDigit)]);

    private static string DescribeTaxCode(string taxCode) => taxCode switch
    {
        TaxCodes.Normal => "Taxa normal",
        TaxCodes.Intermediate => "Taxa intermédia",
        TaxCodes.Reduced => "Taxa reduzida",
        TaxCodes.Exempt => "Isento",
        _ => taxCode
    };
}
