namespace Erp.FiscalPT.Saft;

/// <summary>An address as the SAF-T file carries it. Only AddressDetail and Country are required.</summary>
public sealed class SaftAddress
{
    public string AddressDetail { get; init; } = SaftConstants.Unknown;

    public string City { get; init; } = SaftConstants.Unknown;

    public string? PostalCode { get; init; }

    public string Country { get; init; } = SaftConstants.CountryDefault;
}
