namespace Erp.Sales.Domain;

/// <summary>Counterparty of a movement: a customer, or a supplier on a return.</summary>
/// <param name="TaxId">999999990 stands for an unidentified final consumer.</param>
public sealed record MovementParty(string TaxId, string Name, bool IsSupplier = false);
