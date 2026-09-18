namespace Erp.Main.Models.Core;

/// <summary>
/// Editable state of a customer or supplier form. Mutable on purpose: the shared field component
/// binds straight to it.
/// </summary>
public sealed class PartnerFormModel
{
    /// <summary>Stands for an unidentified final consumer — the fallback when no tax id is given.</summary>
    public const string FinalConsumerTaxId = "999999990";

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = "PT";
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The code is not checked: it is never typed on screen, and a new record is numbered by the API.
    /// </summary>
    public bool IsValid(bool requireTaxId = true) =>
        !string.IsNullOrWhiteSpace(Name)
        && (!requireTaxId || !string.IsNullOrWhiteSpace(TaxId))
        && Erp.Common.PostalCodes.IsValid(PostalCode, Country);

    /// <summary>
    /// Fills in the tax id when it was left blank — for a customer, where it is optional in the UI
    /// even though the API still requires one. Called right before saving.
    /// </summary>
    public void ApplyDefaults()
    {
        if (string.IsNullOrWhiteSpace(TaxId))
            TaxId = FinalConsumerTaxId;
    }

    public static PartnerFormModel From(Partner partner) => new()
    {
        Code = partner.Code,
        Name = partner.Name,
        TaxId = partner.TaxId,
        Address = partner.Address ?? string.Empty,
        PostalCode = partner.PostalCode ?? string.Empty,
        City = partner.City ?? string.Empty,
        Country = partner.Country,
        Email = partner.Email ?? string.Empty,
        Phone = partner.Phone ?? string.Empty,
        IsActive = partner.IsActive
    };

    public CreatePartnerRequest ToCreateRequest(Guid companyId) => new(
        companyId,
        Code.Trim(),
        Name.Trim(),
        TaxId.Trim(),
        Normalize(Address),
        Normalize(PostalCode),
        Normalize(City),
        Country,
        Normalize(Email),
        Normalize(Phone));

    public UpdatePartnerRequest ToUpdateRequest() => new(
        Name.Trim(),
        TaxId.Trim(),
        Normalize(Address),
        Normalize(PostalCode),
        Normalize(City),
        Country,
        Normalize(Email),
        Normalize(Phone),
        IsActive);

    private static string? Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
