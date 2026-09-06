namespace Erp.FiscalPT.QrCode;

/// <summary>
/// Taxable base and VAT for one rate within one fiscal space, as it goes into the QR code.
/// </summary>
/// <param name="TaxCountryRegion">PT, PT-AC or PT-MA.</param>
/// <param name="TaxCode">RED, INT, NOR or ISE.</param>
public sealed record QrCodeTaxAmount(string TaxCountryRegion, string TaxCode, decimal TaxableBase, decimal TaxAmount);
