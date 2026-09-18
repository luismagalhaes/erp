using Erp.Main.Models.Core;

namespace Erp.Main.Services;

/// <summary>
/// Free, external master-data lookups: address by postal code, and company data/validity by
/// NIF/NIPC. Used by the customer/supplier forms and the inline customer section of an invoice.
/// </summary>
public sealed class LookupApiClient(HttpClient http) : ApiClientBase(http)
{
    /// <summary>Null when the code has no match (a 404 — not every postal code is registered).</summary>
    public Task<PostalCodeLookupResult?> GetPostalCodeAsync(string postalCode, CancellationToken ct = default) =>
        GetSingleAsync<PostalCodeLookupResult>($"api/lookups/postal-codes/{Uri.EscapeDataString(postalCode)}", ct);

    public Task<VatNumberValidationResult?> GetVatNumberAsync(string nif, CancellationToken ct = default) =>
        GetSingleAsync<VatNumberValidationResult>($"api/lookups/vat-numbers/{Uri.EscapeDataString(nif)}", ct);
}
