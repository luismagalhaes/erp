namespace Erp.Sales.Infrastructure.Contracts;

/// <param name="TaxId">Leave empty for an unidentified final consumer.</param>
public sealed record MovementPartyRequest(string? TaxId, string Name, bool IsSupplier = false);
