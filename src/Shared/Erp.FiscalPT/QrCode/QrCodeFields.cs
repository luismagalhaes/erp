namespace Erp.FiscalPT.QrCode;

/// <summary>
/// Values that make up the QR code message required by Portaria 195/2020.
/// Field letters are assigned by <see cref="QrCodePayloadBuilder"/>.
/// </summary>
public sealed record QrCodeFields
{
    /// <summary>Field A - issuer tax id.</summary>
    public required string IssuerTaxId { get; init; }

    /// <summary>Field B - buyer tax id. Use 999999990 for an unidentified final consumer.</summary>
    public string BuyerTaxId { get; init; } = "999999990";

    /// <summary>Field C - buyer country.</summary>
    public string BuyerCountry { get; init; } = "PT";

    /// <summary>Field D - document type (FT, FS, NC, ...).</summary>
    public required string DocumentType { get; init; }

    /// <summary>Field E - document status (N, A, F, ...).</summary>
    public required string DocumentStatus { get; init; }

    /// <summary>Field F - document date.</summary>
    public required DateOnly DocumentDate { get; init; }

    /// <summary>Field G - unique document identifier, e.g. "FT A2026/2".</summary>
    public required string DocumentNumber { get; init; }

    /// <summary>Field H - ATCUD without the "ATCUD:" prefix.</summary>
    public required string Atcud { get; init; }

    /// <summary>Taxable bases and VAT per fiscal space and rate (fields I, J and K).</summary>
    public IReadOnlyList<QrCodeTaxAmount> TaxAmounts { get; init; } = [];

    /// <summary>Field N - total taxes.</summary>
    public decimal TotalTaxes { get; init; }

    /// <summary>Field O - document total with taxes.</summary>
    public required decimal GrossTotal { get; init; }

    /// <summary>Field Q - four characters of the signature.</summary>
    public required string HashCharacters { get; init; }

    /// <summary>Field R - certificate number issued by the tax authority.</summary>
    public required string CertificateNumber { get; init; }

    /// <summary>Field S - other information.</summary>
    public string? OtherInformation { get; init; }
}
