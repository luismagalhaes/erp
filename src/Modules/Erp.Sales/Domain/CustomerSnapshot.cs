namespace Erp.Sales.Domain;

/// <summary>
/// Customer data as it was at issuing time. Copied into the document instead of referenced,
/// so later changes to the customer master file cannot rewrite issued documents.
/// </summary>
/// <param name="TaxId">Buyer tax id; 999999990 for an unidentified final consumer.</param>
public sealed record CustomerSnapshot(
    string TaxId,
    string Name,
    string? Address,
    string Country = "PT",
    string? PostalCode = null,
    string? City = null)
{
    public const string FinalConsumerTaxId = "999999990";

    public static CustomerSnapshot FinalConsumer() =>
        new(FinalConsumerTaxId, "Consumidor final", null);
}
