namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>What was found for one line of a count.</summary>
public sealed record CountedLineRequest(Guid LineId, decimal CountedQuantity);
