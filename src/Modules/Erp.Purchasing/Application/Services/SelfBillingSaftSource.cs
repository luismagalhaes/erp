using Erp.FiscalPT.Documents;
using Erp.FiscalPT.Saft;
using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;

namespace Erp.Purchasing.Application.Services;

/// <summary>
/// What this module puts into the SAF-T file of type <c>"S"</c>: the invoices we issued in a
/// supplier's name.
/// </summary>
/// <remarks>
/// The file is the <b>supplier's</b> — their tax id is in the header, and there is one file per
/// supplier — which is why every request here is narrowed by
/// <see cref="SaftSourceRequest.SubjectTaxId"/>. A request without it would produce one file mixing
/// several suppliers' sales, which is not a document the tax authority has a use for.
/// <para>
/// The <c>Customer</c> table holds a single entry: us, with <c>SelfBillingIndicator = 1</c>. It
/// reads oddly until you look from the right side — the document titles a sale of the supplier's,
/// and in that sale we are the customer.
/// </para>
/// </remarks>
public sealed class SelfBillingSaftSource(ISelfBilledInvoiceStorage invoiceStorage) : ISaftDocumentSource
{
    public string FileType => SaftFileType.SelfBilling;

    public async Task<SaftSourceContent> GetContentAsync(
        SaftSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SubjectTaxId))
        {
            throw new ArgumentException(
                "A self-billing file is produced per supplier, so the supplier's tax id is required.",
                nameof(request));
        }

        if (request.SelfBiller is null)
        {
            throw new ArgumentException(
                "A self-billing file needs the self-biller: they are the customer of the sales in it.",
                nameof(request));
        }

        var invoices = await invoiceStorage.GetForPeriodAsync(
            request.CompanyId, request.StartDate, request.EndDate, request.SubjectTaxId, cancellationToken);

        return new SaftSourceContent(
            [.. invoices.Select(invoice => MapInvoice(invoice, request.SelfBiller))],
            [],
            [],
            [BuildSelfBiller(request.SelfBiller)],
            BuildProducts(invoices),
            BuildTaxTable(invoices));
    }

    /// <summary>The only customer these sales have: whoever issued them on the supplier's behalf.</summary>
    private static SaftCustomer BuildSelfBiller(SaftEntityInfo selfBiller) =>
        new()
        {
            CustomerId = selfBiller.TaxId,
            CustomerTaxId = selfBiller.TaxId,
            CompanyName = string.IsNullOrWhiteSpace(selfBiller.LegalName) ? selfBiller.Name : selfBiller.LegalName,
            BillingAddress = new SaftAddress
            {
                AddressDetail = selfBiller.Address ?? SaftConstants.Unknown,
                City = selfBiller.City ?? SaftConstants.Unknown,
                PostalCode = selfBiller.PostalCode,
                Country = selfBiller.Country
            },
            SelfBilling = true
        };

    private static List<SaftProduct> BuildProducts(IReadOnlyList<SelfBilledInvoice> invoices) =>
        [.. invoices
            .SelectMany(invoice => invoice.Lines)
            .Where(line => !string.IsNullOrWhiteSpace(line.ProductCode))
            .GroupBy(line => line.ProductCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new SaftProduct
            {
                ProductCode = group.Key,
                ProductDescription = group.First().ProductDescription,
                ProductNumberCode = group.Key
            })];

    /// <summary>
    /// Only the rates the documents actually reference. Declaring more would leave the table and the
    /// lines disagreeing, which is what the validator checks.
    /// </summary>
    private static List<SaftTaxTableEntry> BuildTaxTable(IReadOnlyList<SelfBilledInvoice> invoices) =>
        [.. invoices
            .SelectMany(invoice => invoice.Lines)
            .GroupBy(line => (line.TaxCountryRegion, line.TaxCode, line.TaxPercentage))
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

    private static SaftInvoice MapInvoice(SelfBilledInvoice invoice, SaftEntityInfo selfBiller) =>
        new()
        {
            InvoiceNo = invoice.DocumentNumber,
            Atcud = invoice.Atcud,
            DocumentStatus = BuildStatus(invoice),
            Hash = invoice.Hash,
            HashControl = invoice.HashControl,
            Period = invoice.IssueDate.Month,
            InvoiceDate = invoice.IssueDate,
            InvoiceType = invoice.DocumentType,
            // What makes this file what it is, on every document in it.
            SelfBilling = true,
            SourceId = SaftSourceId.Fit(invoice.CreatedByUserId),
            SystemEntryDate = invoice.SystemEntryDateUtc,
            CustomerId = selfBiller.TaxId,
            Lines = [.. invoice.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line => new SaftInvoiceLine
                {
                    LineNumber = line.LineNumber,
                    ProductCode = line.ProductCode,
                    ProductDescription = line.ProductDescription,
                    Quantity = line.Quantity,
                    UnitOfMeasure = line.UnitOfMeasure,
                    UnitPrice = line.UnitPrice,
                    TaxPointDate = invoice.IssueDate,
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
                TaxPayable = invoice.TaxPayable,
                NetTotal = invoice.NetTotal,
                GrossTotal = invoice.GrossTotal
            }
        };

    /// <summary>
    /// A voided document stays in the file with status "A" and the reason it was voided — that is
    /// the whole point of voiding by appending rather than deleting.
    /// </summary>
    private static SaftDocumentStatus BuildStatus(SelfBilledInvoice invoice)
    {
        var latest = invoice.StatusChanges.OrderByDescending(x => x.OccurredAtUtc).FirstOrDefault();

        return new SaftDocumentStatus
        {
            Status = invoice.EffectiveStatus,
            StatusDate = latest?.OccurredAtUtc ?? invoice.SystemEntryDateUtc,
            Reason = latest?.Reason,
            SourceId = SaftSourceId.Fit(latest?.UserId ?? invoice.CreatedByUserId),
            SourceBilling = SourceBillingTypes.Produced
        };
    }
}
