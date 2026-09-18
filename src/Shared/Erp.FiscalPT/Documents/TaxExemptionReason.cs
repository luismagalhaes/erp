namespace Erp.FiscalPT.Documents;

/// <summary>One entry of the tax authority's table of VAT exemption reasons.</summary>
/// <param name="Code">SAF-T TaxExemptionCode, e.g. "M07".</param>
/// <param name="LegalBasis">
/// The legal mention printed on the document and exported as SAF-T TaxExemptionReason. Never longer
/// than the 60 characters the schema allows.
/// </param>
/// <param name="Description">What the reason is, for someone choosing it.</param>
public sealed record TaxExemptionReason(string Code, string LegalBasis, string Description);
