using Erp.FiscalPT.Documents;
using Erp.FiscalPT.Saft;
using Erp.Sales.Domain;
using Erp.Sales.Infrastructure.Storage;

namespace Erp.Sales.Application.Services;

/// <summary>
/// What the Sales module puts into the SAF-T file of documents we issued in our own name.
/// </summary>
/// <remarks>
/// Everything that knows what a <see cref="SalesDocument"/> is lives here. The file itself is
/// assembled elsewhere, from this and from whatever other modules contribute - which is what lets
/// self-billing have its own file without this one knowing anything about it.
/// </remarks>
public sealed class SalesSaftSource(
    ISalesDocumentStorage documentStorage,
    IStockMovementStorage movementStorage,
    IPaymentStorage paymentStorage) : ISaftDocumentSource
{
    public string FileType => SaftFileType.Billing;

    public async Task<SaftSourceContent> GetContentAsync(
        SaftSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var documents = await documentStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        var movements = await movementStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        var payments = await paymentStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, cancellationToken);

        return new SaftSourceContent(
            [.. documents.Select(MapInvoice)],
            [.. movements.Select(MapStockMovement)],
            [.. payments.Select(MapPayment)],
            BuildCustomers(documents, movements, payments),
            BuildProducts(documents, movements),
            BuildTaxTable(documents, movements));
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
        // Receipts and movements carry no address, so they rank below any document of the same day:
        // the address exported is the latest one a document actually recorded.
        var parties = documents
            .Select(x => (TaxId: x.CustomerTaxId, x.CustomerName, Address: x.CustomerAddress,
                PostalCode: x.CustomerPostalCode, City: x.CustomerCity, Date: x.DocumentDate, Rank: 1))
            .Concat(movements
                .Where(x => !x.PartyIsSupplier)
                .Select(x => (TaxId: x.PartyTaxId, CustomerName: x.PartyName, Address: (string?)null,
                    PostalCode: (string?)null, City: (string?)null, Date: x.MovementDate, Rank: 0)))
            .Concat(payments
                .Select(x => (TaxId: x.PartyTaxId, CustomerName: x.PartyName, Address: (string?)null,
                    PostalCode: (string?)null, City: (string?)null, Date: x.TransactionDate, Rank: 0)));

        return [.. parties
            .Where(party => !string.IsNullOrWhiteSpace(party.TaxId))
            .GroupBy(party => party.TaxId, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(party => party.Date).ThenByDescending(party => party.Rank).First())
            .OrderBy(party => party.TaxId, StringComparer.Ordinal)
            .Select(party => new SaftCustomer
            {
                CustomerId = party.TaxId,
                CustomerTaxId = party.TaxId,
                CompanyName = party.CustomerName,
                BillingAddress = new SaftAddress
                {
                    AddressDetail = party.Address ?? SaftConstants.Unknown,
                    City = party.City ?? SaftConstants.Unknown,
                    PostalCode = party.PostalCode
                }
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
                Description = SaftTaxDescriptions.For(rate.TaxCode),
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
            SourceId = SaftSourceId.Fit(document.CreatedByUserId),
            SystemEntryDate = document.SystemEntryDateUtc,
            CustomerId = document.CustomerTaxId,
            Lines = [.. document.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SaftInvoiceLine
                {
                    LineNumber = line.LineNumber,
                    // Set when the line invoices a delivery note.
                    OriginatingOn = line.OriginatingNumber,
                    OrderDate = line.OriginatingDate,
                    ProductCode = line.ProductCode,
                    ProductDescription = line.ProductDescription,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    // SAF-T wants the price net of the line discount, so that quantity times price
                    // gives the amount; the discount itself travels as SettlementAmount.
                    UnitPrice = line.NetUnitPrice,
                    SettlementAmount = line.DiscountAmount,
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
                GrossTotal = document.GrossTotal,
                Payments = [.. document.Payments.Select(payment => new SaftPaymentMethod
                {
                    PaymentMechanism = payment.Mechanism,
                    PaymentAmount = payment.Amount,
                    PaymentDate = payment.PaymentDate
                })]
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
            SourceId = SaftSourceId.Fit(movement.CreatedByUserId),
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
                    // Net of the line discount, like on an invoice.
                    UnitPrice = line.NetUnitPrice,
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
            SourceId = SaftSourceId.Fit(payment.CreatedByUserId),
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
            SourceId = SaftSourceId.Fit(voided ? lastChange?.UserId ?? createdByUserId : createdByUserId),
            SourceBilling = sourceBilling
        };
    }
}