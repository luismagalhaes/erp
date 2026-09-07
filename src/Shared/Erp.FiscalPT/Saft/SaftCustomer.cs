namespace Erp.FiscalPT.Saft;

/// <summary>A customer in MasterFiles. Every CustomerID used in the documents must appear here.</summary>
public sealed class SaftCustomer
{
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>Account in the chart of accounts, or "Desconhecido" when accounting is not integrated.</summary>
    public string AccountId { get; init; } = SaftConstants.Unknown;

    public string CustomerTaxId { get; init; } = string.Empty;

    public string CompanyName { get; init; } = string.Empty;

    public SaftAddress BillingAddress { get; init; } = new();

    /// <summary>1 when the customer self-bills on behalf of the entity.</summary>
    public bool SelfBilling { get; init; }
}
