using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Erp.FiscalPT.Saft;

/// <summary>
/// Writes the SAF-T (PT) file. Element order matters — the tax authority validates against the
/// XSD — so the order here follows the schema, and the control totals are computed from the
/// documents instead of being taken from the caller, which is the only way they cannot disagree.
/// </summary>
public static class SaftXmlWriter
{
    private static readonly XNamespace Ns = SaftConstants.Namespace;

    /// <summary>Builds the document tree.</summary>
    public static XDocument Build(SaftAuditFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var auditFile = new XElement(Ns + "AuditFile",
            BuildHeader(file.Header),
            BuildMasterFiles(file),
            BuildSourceDocuments(file));

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), auditFile);
    }

    /// <summary>Serializes the file as UTF-8 bytes, without a byte order mark.</summary>
    public static byte[] Serialize(SaftAuditFile file)
    {
        using var stream = new MemoryStream();
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            Build(file).Save(writer, SaveOptions.None);
        }

        return stream.ToArray();
    }

    /// <summary>
    /// File name the tax authority expects: SAFT + tax id + period, e.g.
    /// <c>SAFT_123456789_20260101_20260131.xml</c>.
    /// </summary>
    public static string BuildFileName(SaftHeader header) =>
        $"SAFT_{header.TaxRegistrationNumber}_{header.StartDate:yyyyMMdd}_{header.EndDate:yyyyMMdd}.xml";

    private static XElement BuildHeader(SaftHeader header)
    {
        return new XElement(Ns + "Header",
            new XElement(Ns + "AuditFileVersion", SaftConstants.AuditFileVersion),
            new XElement(Ns + "CompanyID", header.CompanyId),
            new XElement(Ns + "TaxRegistrationNumber", header.TaxRegistrationNumber),
            new XElement(Ns + "TaxAccountingBasis", header.TaxAccountingBasis),
            new XElement(Ns + "CompanyName", header.CompanyName),
            Optional("BusinessName", header.BusinessName),
            BuildAddress("CompanyAddress", header.CompanyAddress),
            new XElement(Ns + "FiscalYear", header.FiscalYear),
            new XElement(Ns + "StartDate", Date(header.StartDate)),
            new XElement(Ns + "EndDate", Date(header.EndDate)),
            new XElement(Ns + "CurrencyCode", SaftConstants.CurrencyCode),
            new XElement(Ns + "DateCreated", Date(header.DateCreated)),
            new XElement(Ns + "TaxEntity", SaftConstants.TaxEntityGlobal),
            new XElement(Ns + "ProductCompanyTaxID", header.ProductCompanyTaxId),
            new XElement(Ns + "SoftwareCertificateNumber", header.SoftwareCertificateNumber),
            new XElement(Ns + "ProductID", header.ProductId),
            new XElement(Ns + "ProductVersion", header.ProductVersion),
            Optional("HeaderComment", header.HeaderComment),
            Optional("Telephone", header.Telephone),
            Optional("Email", header.Email));
    }

    private static XElement BuildMasterFiles(SaftAuditFile file)
    {
        var masterFiles = new XElement(Ns + "MasterFiles");

        foreach (var customer in file.Customers)
        {
            masterFiles.Add(new XElement(Ns + "Customer",
                new XElement(Ns + "CustomerID", customer.CustomerId),
                new XElement(Ns + "AccountID", customer.AccountId),
                new XElement(Ns + "CustomerTaxID", customer.CustomerTaxId),
                new XElement(Ns + "CompanyName", customer.CompanyName),
                BuildAddress("BillingAddress", customer.BillingAddress),
                new XElement(Ns + "SelfBillingIndicator", Flag(customer.SelfBilling))));
        }

        foreach (var product in file.Products)
        {
            masterFiles.Add(new XElement(Ns + "Product",
                new XElement(Ns + "ProductType", product.ProductType),
                new XElement(Ns + "ProductCode", product.ProductCode),
                new XElement(Ns + "ProductDescription", product.ProductDescription),
                new XElement(Ns + "ProductNumberCode", product.ProductNumberCode)));
        }

        // TaxTable is optional, but an empty one is invalid: it needs at least one entry.
        if (file.TaxTable.Count > 0)
        {
            var taxTable = new XElement(Ns + "TaxTable");

            foreach (var entry in file.TaxTable)
            {
                taxTable.Add(new XElement(Ns + "TaxTableEntry",
                    new XElement(Ns + "TaxType", entry.TaxType),
                    new XElement(Ns + "TaxCountryRegion", entry.TaxCountryRegion),
                    new XElement(Ns + "TaxCode", entry.TaxCode),
                    new XElement(Ns + "Description", entry.Description),
                    new XElement(Ns + "TaxPercentage", Money(entry.TaxPercentage))));
            }

            masterFiles.Add(taxTable);
        }

        return masterFiles;
    }

    private static XElement BuildSourceDocuments(SaftAuditFile file)
    {
        var sourceDocuments = new XElement(Ns + "SourceDocuments");

        if (file.Invoices.Count > 0)
            sourceDocuments.Add(BuildSalesInvoices(file.Invoices));

        if (file.StockMovements.Count > 0)
            sourceDocuments.Add(BuildMovementOfGoods(file.StockMovements));

        if (file.Payments.Count > 0)
            sourceDocuments.Add(BuildPayments(file.Payments));

        return sourceDocuments;
    }

    private static XElement BuildSalesInvoices(IReadOnlyList<SaftInvoice> invoices)
    {
        // Voided documents stay in the file but do not count towards the totals.
        var live = invoices.Where(invoice => invoice.DocumentStatus.Status != "A").ToList();

        var debit = live.Where(invoice => SaftConstants.IsDebitDocument(invoice.InvoiceType))
            .Sum(invoice => invoice.Totals.NetTotal);

        var credit = live.Where(invoice => !SaftConstants.IsDebitDocument(invoice.InvoiceType))
            .Sum(invoice => invoice.Totals.NetTotal);

        var element = new XElement(Ns + "SalesInvoices",
            new XElement(Ns + "NumberOfEntries", invoices.Count),
            new XElement(Ns + "TotalDebit", Money(debit)),
            new XElement(Ns + "TotalCredit", Money(credit)));

        foreach (var invoice in invoices)
            element.Add(BuildInvoice(invoice));

        return element;
    }

    private static XElement BuildInvoice(SaftInvoice invoice)
    {
        var amountElement = SaftConstants.IsDebitDocument(invoice.InvoiceType) ? "DebitAmount" : "CreditAmount";

        var element = new XElement(Ns + "Invoice",
            new XElement(Ns + "InvoiceNo", invoice.InvoiceNo),
            new XElement(Ns + "ATCUD", invoice.Atcud),
            new XElement(Ns + "DocumentStatus",
                new XElement(Ns + "InvoiceStatus", invoice.DocumentStatus.Status),
                new XElement(Ns + "InvoiceStatusDate", DateTimeValue(invoice.DocumentStatus.StatusDate)),
                Optional("Reason", invoice.DocumentStatus.Reason),
                new XElement(Ns + "SourceID", invoice.DocumentStatus.SourceId),
                new XElement(Ns + "SourceBilling", invoice.DocumentStatus.SourceBilling)),
            new XElement(Ns + "Hash", invoice.Hash),
            new XElement(Ns + "HashControl", invoice.HashControl),
            new XElement(Ns + "Period", invoice.Period),
            new XElement(Ns + "InvoiceDate", Date(invoice.InvoiceDate)),
            new XElement(Ns + "InvoiceType", invoice.InvoiceType),
            new XElement(Ns + "SpecialRegimes",
                new XElement(Ns + "SelfBillingIndicator", Flag(invoice.SelfBilling)),
                new XElement(Ns + "CashVATSchemeIndicator", Flag(invoice.CashVatScheme)),
                new XElement(Ns + "ThirdPartiesBillingIndicator", Flag(invoice.ThirdPartiesBilling))),
            new XElement(Ns + "SourceID", invoice.SourceId),
            new XElement(Ns + "SystemEntryDate", DateTimeValue(invoice.SystemEntryDate)),
            new XElement(Ns + "CustomerID", invoice.CustomerId));

        foreach (var line in invoice.Lines)
        {
            element.Add(new XElement(Ns + "Line",
                new XElement(Ns + "LineNumber", line.LineNumber),
                BuildOrderReferences(line),
                new XElement(Ns + "ProductCode", line.ProductCode),
                new XElement(Ns + "ProductDescription", line.ProductDescription),
                new XElement(Ns + "Quantity", Quantity(line.Quantity)),
                new XElement(Ns + "UnitOfMeasure", line.UnitOfMeasure),
                new XElement(Ns + "UnitPrice", Quantity(line.UnitPrice)),
                new XElement(Ns + "TaxPointDate", Date(line.TaxPointDate)),
                BuildReferences(line),
                new XElement(Ns + "Description", line.Description),
                new XElement(Ns + amountElement, Money(line.Amount)),
                BuildTax(line.Tax),
                Optional("TaxExemptionReason", line.TaxExemptionReason),
                Optional("TaxExemptionCode", line.TaxExemptionCode),
                line.SettlementAmount == 0 ? null : new XElement(Ns + "SettlementAmount", Money(line.SettlementAmount))));
        }

        element.Add(BuildTotals(invoice.Totals));

        return element;
    }

    private static XElement BuildMovementOfGoods(IReadOnlyList<SaftStockMovement> movements)
    {
        var live = movements.Where(movement => movement.DocumentStatus.Status != "A").ToList();

        var element = new XElement(Ns + "MovementOfGoods",
            new XElement(Ns + "NumberOfMovementLines", movements.Sum(movement => movement.Lines.Count)),
            new XElement(Ns + "TotalQuantityIssued",
                Quantity(live.SelectMany(movement => movement.Lines).Sum(line => line.Quantity))));

        foreach (var movement in movements)
            element.Add(BuildStockMovement(movement));

        return element;
    }

    private static XElement BuildStockMovement(SaftStockMovement movement)
    {
        var element = new XElement(Ns + "StockMovement",
            new XElement(Ns + "DocumentNumber", movement.DocumentNumber),
            new XElement(Ns + "ATCUD", movement.Atcud),
            new XElement(Ns + "DocumentStatus",
                new XElement(Ns + "MovementStatus", movement.DocumentStatus.Status),
                new XElement(Ns + "MovementStatusDate", DateTimeValue(movement.DocumentStatus.StatusDate)),
                Optional("Reason", movement.DocumentStatus.Reason),
                new XElement(Ns + "SourceID", movement.DocumentStatus.SourceId),
                new XElement(Ns + "SourceBilling", movement.DocumentStatus.SourceBilling)),
            new XElement(Ns + "Hash", movement.Hash),
            new XElement(Ns + "HashControl", movement.HashControl),
            new XElement(Ns + "Period", movement.Period),
            new XElement(Ns + "MovementDate", Date(movement.MovementDate)),
            new XElement(Ns + "MovementType", movement.MovementType),
            new XElement(Ns + "SystemEntryDate", DateTimeValue(movement.SystemEntryDate)),
            new XElement(Ns + (movement.PartyIsSupplier ? "SupplierID" : "CustomerID"), movement.PartyId),
            new XElement(Ns + "SourceID", movement.SourceId),
            Optional("MovementComments", movement.MovementComments),
            BuildShippingPoint("ShipTo", movement.ShipTo),
            BuildShippingPoint("ShipFrom", movement.ShipFrom),
            movement.MovementEndTime is null
                ? null
                : new XElement(Ns + "MovementEndTime", DateTimeValue(movement.MovementEndTime.Value)),
            new XElement(Ns + "MovementStartTime", DateTimeValue(movement.MovementStartTime)),
            Optional("ATDocCodeID", movement.AtDocCodeId));

        foreach (var line in movement.Lines)
        {
            element.Add(new XElement(Ns + "Line",
                new XElement(Ns + "LineNumber", line.LineNumber),
                new XElement(Ns + "ProductCode", line.ProductCode),
                new XElement(Ns + "ProductDescription", line.ProductDescription),
                new XElement(Ns + "Quantity", Quantity(line.Quantity)),
                new XElement(Ns + "UnitOfMeasure", line.UnitOfMeasure),
                new XElement(Ns + "UnitPrice", Quantity(line.UnitPrice)),
                new XElement(Ns + "Description", line.Description),
                new XElement(Ns + "CreditAmount", Money(line.Amount)),
                BuildTax(line.Tax),
                Optional("TaxExemptionReason", line.TaxExemptionReason),
                Optional("TaxExemptionCode", line.TaxExemptionCode)));
        }

        element.Add(BuildTotals(movement.Totals));

        return element;
    }

    private static XElement BuildPayments(IReadOnlyList<SaftPayment> payments)
    {
        var live = payments.Where(payment => payment.DocumentStatus.Status != "A").ToList();

        var element = new XElement(Ns + "Payments",
            new XElement(Ns + "NumberOfEntries", payments.Count),
            new XElement(Ns + "TotalDebit", Money(0m)),
            new XElement(Ns + "TotalCredit", Money(live.Sum(payment => payment.Totals.NetTotal))));

        foreach (var payment in payments)
            element.Add(BuildPayment(payment));

        return element;
    }

    private static XElement BuildPayment(SaftPayment payment)
    {
        var element = new XElement(Ns + "Payment",
            new XElement(Ns + "PaymentRefNo", payment.PaymentRefNo),
            new XElement(Ns + "ATCUD", payment.Atcud),
            new XElement(Ns + "Period", payment.Period),
            new XElement(Ns + "TransactionDate", Date(payment.TransactionDate)),
            new XElement(Ns + "PaymentType", payment.PaymentType),
            Optional("Description", payment.Description),
            new XElement(Ns + "DocumentStatus",
                new XElement(Ns + "PaymentStatus", payment.DocumentStatus.Status),
                new XElement(Ns + "PaymentStatusDate", DateTimeValue(payment.DocumentStatus.StatusDate)),
                Optional("Reason", payment.DocumentStatus.Reason),
                new XElement(Ns + "SourceID", payment.DocumentStatus.SourceId),
                new XElement(Ns + "SourcePayment", payment.DocumentStatus.SourceBilling)));

        foreach (var method in payment.PaymentMethods)
        {
            element.Add(new XElement(Ns + "PaymentMethod",
                new XElement(Ns + "PaymentMechanism", method.PaymentMechanism),
                new XElement(Ns + "PaymentAmount", Money(method.PaymentAmount)),
                new XElement(Ns + "PaymentDate", Date(method.PaymentDate))));
        }

        element.Add(new XElement(Ns + "SourceID", payment.SourceId));
        element.Add(new XElement(Ns + "SystemEntryDate", DateTimeValue(payment.SystemEntryDate)));
        element.Add(new XElement(Ns + "CustomerID", payment.CustomerId));

        foreach (var line in payment.Lines)
        {
            element.Add(new XElement(Ns + "Line",
                new XElement(Ns + "LineNumber", line.LineNumber),
                new XElement(Ns + "SourceDocumentID",
                    new XElement(Ns + "OriginatingON", line.OriginatingOn),
                    new XElement(Ns + "InvoiceDate", Date(line.InvoiceDate))),
                new XElement(Ns + "CreditAmount", Money(line.Amount))));
        }

        element.Add(BuildTotals(payment.Totals));

        return element;
    }

    /// <summary>
    /// OrderReferences, present only on a line that invoices a delivery note. The schema puts it
    /// right after LineNumber.
    /// </summary>
    private static XElement? BuildOrderReferences(SaftInvoiceLine line)
    {
        if (string.IsNullOrWhiteSpace(line.OriginatingOn))
            return null;

        return new XElement(Ns + "OrderReferences",
            new XElement(Ns + "OriginatingON", line.OriginatingOn),
            line.OrderDate is null ? null : new XElement(Ns + "OrderDate", Date(line.OrderDate.Value)));
    }

    /// <summary>
    /// References/Reference and Reason, present only on a line that corrects another document.
    /// The element sits between TaxPointDate and Description, where the schema puts it.
    /// </summary>
    private static XElement? BuildReferences(SaftInvoiceLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Reference) && string.IsNullOrWhiteSpace(line.ReferenceReason))
            return null;

        return new XElement(Ns + "References",
            Optional("Reference", line.Reference),
            Optional("Reason", line.ReferenceReason));
    }

    private static XElement BuildTax(SaftTax tax) =>
        new(Ns + "Tax",
            new XElement(Ns + "TaxType", tax.TaxType),
            new XElement(Ns + "TaxCountryRegion", tax.TaxCountryRegion),
            new XElement(Ns + "TaxCode", tax.TaxCode),
            new XElement(Ns + "TaxPercentage", Money(tax.TaxPercentage)));

    private static XElement BuildTotals(SaftDocumentTotals totals) =>
        new(Ns + "DocumentTotals",
            new XElement(Ns + "TaxPayable", Money(totals.TaxPayable)),
            new XElement(Ns + "NetTotal", Money(totals.NetTotal)),
            new XElement(Ns + "GrossTotal", Money(totals.GrossTotal)),
            totals.Payments.Select(method => new XElement(Ns + "Payment",
                new XElement(Ns + "PaymentMechanism", method.PaymentMechanism),
                new XElement(Ns + "PaymentAmount", Money(method.PaymentAmount)),
                new XElement(Ns + "PaymentDate", Date(method.PaymentDate)))));

    private static XElement BuildShippingPoint(string name, SaftShippingPoint point)
    {
        return new XElement(Ns + name,
            Optional("WarehouseID", point.WarehouseId),
            Optional("LocationID", point.LocationId),
            BuildAddress("Address", point.Address));
    }

    private static XElement BuildAddress(string name, SaftAddress address)
    {
        return new XElement(Ns + name,
            new XElement(Ns + "AddressDetail", Fallback(address.AddressDetail)),
            new XElement(Ns + "City", Fallback(address.City)),
            new XElement(Ns + "PostalCode", Fallback(address.PostalCode)),
            new XElement(Ns + "Country", string.IsNullOrWhiteSpace(address.Country)
                ? SaftConstants.CountryDefault
                : address.Country));
    }

    /// <summary>Optional elements are left out entirely when there is nothing to say.</summary>
    private static XElement? Optional(string name, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new XElement(Ns + name, value.Trim());

    /// <summary>Required elements with no value carry the placeholder the tax authority expects.</summary>
    private static string Fallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? SaftConstants.Unknown : value.Trim();

    private static string Flag(bool value) => value ? "1" : "0";

    private static string Money(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string DateTimeValue(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
}
