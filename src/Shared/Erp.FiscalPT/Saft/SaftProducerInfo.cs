namespace Erp.FiscalPT.Saft;

/// <summary>
/// Who made the program, as registered with the tax authority. Goes in the header of every file and
/// has nothing to do with whose documents are in it.
/// </summary>
/// <param name="ProductId">"ProductName/CompanyName", in the form that was registered.</param>
public sealed record SaftProducerInfo(
    string TaxId,
    string CertificateNumber,
    string ProductId,
    string ProductVersion = "1.0");
