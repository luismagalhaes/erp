namespace Erp.Main.Models;

/// <summary>
/// Editable state of a customer or supplier form. Mutable on purpose: the shared field component
/// binds straight to it.
/// </summary>
public sealed class PartnerFormModel
{
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

    public bool IsValid => !string.IsNullOrWhiteSpace(Code)
                           && !string.IsNullOrWhiteSpace(Name)
                           && !string.IsNullOrWhiteSpace(TaxId);

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
