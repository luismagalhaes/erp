namespace Erp.FiscalPT.Saft;

/// <summary>
/// Identifies the taxable entity, the period covered and the certified program that produced
/// the file.
/// </summary>
public sealed class SaftHeader
{
    /// <summary>Registry number and office, or the tax id when there is none.</summary>
    public string CompanyId { get; init; } = string.Empty;

    /// <summary>Tax id of the taxable entity, digits only.</summary>
    public string TaxRegistrationNumber { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public string? BusinessName { get; init; }

    public SaftAddress CompanyAddress { get; init; } = new();

    public int FiscalYear { get; init; }

    public DateOnly StartDate { get; init; }

    public DateOnly EndDate { get; init; }

    public DateOnly DateCreated { get; init; }

    /// <summary>Tax id of the software producer, as registered with the tax authority.</summary>
    public string ProductCompanyTaxId { get; init; } = string.Empty;

    /// <summary>Certificate number assigned to the program.</summary>
    public string SoftwareCertificateNumber { get; init; } = "0";

    /// <summary>"ProductName/CompanyName", in the form registered with the tax authority.</summary>
    public string ProductId { get; init; } = string.Empty;

    public string ProductVersion { get; init; } = "1.0";

    public string? Telephone { get; init; }

    public string? Email { get; init; }

    public string? HeaderComment { get; init; }
}
