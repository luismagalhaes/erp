namespace Erp.Sales.Infrastructure.Contracts;

public sealed record StockMovementListItemDto(
    Guid Id,
    string DocumentNumber,
    string MovementType,
    string Atcud,
    DateOnly MovementDate,
    string PartyName,
    string PartyTaxId,
    DateTime MovementStartAtUtc,
    decimal TotalQuantity,
    decimal GrossTotal,
    string Status,
    string? AtDocCodeId);
