namespace Erp.Inventory.Infrastructure.Contracts;

/// <param name="Quantity">Signed: positive brought stock in, negative took it out.</param>
/// <param name="RunningBalance">The balance after this entry, for reading the ledger as a story.</param>
public sealed record StockLedgerEntryDto(
    Guid Id,
    Guid WarehouseId,
    string ProductCode,
    string Direction,
    decimal Quantity,
    decimal RunningBalance,
    DateOnly MovementDate,
    DateTime SystemEntryDateUtc,
    string? SourceDocumentType,
    string? SourceDocumentNumber,
    string? Reason);
