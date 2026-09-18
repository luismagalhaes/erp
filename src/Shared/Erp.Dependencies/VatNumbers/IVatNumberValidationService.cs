namespace Erp.Dependencies.VatNumbers;

/// <summary>
/// Validates a Portuguese NIF/NIPC: module-11 structural check first, then — only for a company
/// prefix — a live lookup against the EU VIES REST API.
/// </summary>
public interface IVatNumberValidationService
{
    Task<VatNumberValidationResult> ValidateAsync(string nif, CancellationToken cancellationToken = default);
}
