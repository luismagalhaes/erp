using System.Net.Http.Json;

namespace Erp.Dependencies.VatNumbers;

/// <summary>
/// Portuguese-only NIF validator: <see cref="NifValidator"/> runs first and rejects anything
/// that fails the module-11 check without touching the network; a valid company prefix then
/// goes to the EU VIES REST API (base address set once in <see cref="DependencyInjection"/>).
/// </summary>
public sealed class VatNumberValidationService(HttpClient httpClient) : IVatNumberValidationService
{
    private const string PortugalCountryCode = "PT";

    public async Task<VatNumberValidationResult> ValidateAsync(string nif, CancellationToken cancellationToken = default)
    {
        if (!NifValidator.TryNormalize(nif, out var normalized) || !NifValidator.IsValid(normalized))
            return new VatNumberValidationResult(IsStructurallyValid: false, VatNumberValidationSource.LocalOnly);

        if (!NifValidator.IsCompanyPrefix(normalized))
            return new VatNumberValidationResult(IsStructurallyValid: true, VatNumberValidationSource.LocalOnly);

        var response = await httpClient.GetFromJsonAsync<ViesVatResponse>(
            $"ms/{PortugalCountryCode}/vat/{normalized}",
            cancellationToken);

        var (address, postalCode, city) = ViesAddress.Parse(response?.Address);

        return new VatNumberValidationResult(
            IsStructurallyValid: true,
            VatNumberValidationSource.Vies,
            IsRegisteredInVies: response?.IsValid,
            RegisteredName: response?.Name,
            RegisteredAddress: address,
            RegisteredPostalCode: postalCode,
            RegisteredCity: city);
    }
}
