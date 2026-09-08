namespace Erp.FiscalPT.Saft;

/// <summary>
/// Produces a SAF-T (PT) file: asks the modules that have documents for it, assembles, writes and
/// validates.
/// </summary>
/// <remarks>
/// It lives here, with the rest of the fiscal library, because a SAF-T is a file of the **taxable
/// entity** — not of any one module. It reads nothing itself: the documents come from
/// <see cref="ISaftDocumentSource"/>, and the entity and producer details from the caller. That is
/// what lets a self-billing file (<c>"S"</c>) be produced by the same code as the billing one
/// (<c>"F"</c>) without either knowing about the other.
/// </remarks>
public sealed class SaftExporter(IEnumerable<ISaftDocumentSource> sources)
{
    public async Task<SaftExportOutcome> ExportAsync(
        SaftExportSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(spec.Subject);
        ArgumentNullException.ThrowIfNull(spec.Producer);

        if (spec.EndDate < spec.StartDate)
            throw new ArgumentException("The end date is before the start date.", nameof(spec));

        if (string.IsNullOrWhiteSpace(spec.Subject.TaxId))
            throw new ArgumentException("The entity has no tax id, which the SAF-T header requires.", nameof(spec));

        if (!SaftFileType.IsSupported(spec.FileType))
            throw new ArgumentException($"Unknown SAF-T file type '{spec.FileType}'.", nameof(spec));

        var isSelfBilling = spec.FileType == SaftFileType.SelfBilling;

        // Without it the file has no Customer table: the sales it reports are the supplier's, and
        // the customer in them is us. A "S" file that omitted this would be about nobody.
        if (isSelfBilling && spec.SelfBiller is null)
        {
            throw new ArgumentException(
                "A self-billing file needs the self-biller, who is the customer of the sales it reports.",
                nameof(spec));
        }

        var request = new SaftSourceRequest(
            spec.CompanyId,
            spec.StartDate,
            spec.EndDate,
            spec.FileType,
            // On a billing file the subject is the company itself, so there is nobody to narrow to.
            isSelfBilling ? spec.Subject.TaxId : null,
            isSelfBilling ? spec.SelfBiller : null);

        // Only the sources that serve this file type: a self-billed invoice belongs to a file of
        // its own and must never fall into the billing one.
        var contents = new List<SaftSourceContent>();

        foreach (var source in sources.Where(x => x.FileType == spec.FileType))
            contents.Add(await source.GetContentAsync(request, cancellationToken));

        var file = SaftFileAssembler.Assemble(BuildHeader(spec), contents);
        var document = SaftXmlWriter.Build(file);

        // Validated here rather than only in the tests, so a document the writer was never
        // exercised on cannot reach the tax authority unnoticed.
        var validationErrors = SaftSchemaValidator.Validate(document);

        return new SaftExportOutcome(
            SaftXmlWriter.BuildFileName(file.Header),
            SaftXmlWriter.Serialize(file),
            file.Invoices.Count,
            file.StockMovements.Count,
            file.Payments.Count,
            validationErrors);
    }

    private static SaftHeader BuildHeader(SaftExportSpec spec)
    {
        var subject = spec.Subject;

        return new SaftHeader
        {
            TaxAccountingBasis = spec.FileType,
            // With no registry number on file, the tax id stands in, which the schema allows.
            CompanyId = subject.TaxId,
            TaxRegistrationNumber = OnlyDigits(subject.TaxId),
            CompanyName = string.IsNullOrWhiteSpace(subject.LegalName) ? subject.Name : subject.LegalName,
            BusinessName = subject.Name,
            CompanyAddress = new SaftAddress
            {
                AddressDetail = subject.Address ?? SaftConstants.Unknown,
                City = subject.City ?? SaftConstants.Unknown,
                PostalCode = subject.PostalCode,
                Country = subject.Country
            },
            FiscalYear = spec.StartDate.Year,
            StartDate = spec.StartDate,
            EndDate = spec.EndDate,
            DateCreated = DateOnly.FromDateTime(DateTime.Today),
            ProductCompanyTaxId = OnlyDigits(spec.Producer.TaxId),
            SoftwareCertificateNumber = spec.Producer.CertificateNumber,
            ProductId = spec.Producer.ProductId,
            ProductVersion = spec.Producer.ProductVersion,
            Telephone = subject.Phone,
            Email = subject.Email
        };
    }

    private static string OnlyDigits(string value) => new([.. value.Where(char.IsDigit)]);
}
