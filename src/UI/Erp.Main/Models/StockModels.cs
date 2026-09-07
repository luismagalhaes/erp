namespace Erp.Main.Models;

public sealed record Warehouse(
    Guid Id,
    Guid CompanyId,
    string Code,
    string Name,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsDefault,
    bool IsActive);

public sealed record CreateWarehouseRequest(
    Guid CompanyId,
    string Code,
    string Name,
    string? Address = null,
    string? City = null,
    string? PostalCode = null,
    string Country = "PT",
    bool IsDefault = false);

public sealed record UpdateWarehouseRequest(
    string Name,
    string? Address,
    string? City,
    string? PostalCode,
    string Country,
    bool IsDefault,
    bool IsActive);

public sealed record StockBalance(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Quantity,
    DateTime LastMovementUtc);

/// <param name="Quantity">Signed: positive brought stock in, negative took it out.</param>
public sealed record StockLedgerEntry(
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

public sealed record StockCheckLine(
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal RecordedQuantity,
    decimal LedgerQuantity,
    decimal Difference,
    int EntryCount);

public sealed record StockCheckResult(
    Guid CompanyId,
    DateTime CheckedAtUtc,
    int ProductsChecked,
    int ProductsWithDifference,
    IReadOnlyList<StockCheckLine> Lines);

public sealed record AdjustStockRequest(
    Guid CompanyId,
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal Difference,
    DateOnly MovementDate,
    string Reason);

/// <param name="ProductsWithoutCost">
/// Products reported with a value of zero because the product file carries no cost. The
/// communication requires the valuation, so this is worth seeing before submitting.
/// </param>
public sealed record InventoryFileSummary(
    string FileName,
    int LineCount,
    decimal TotalQuantity,
    decimal TotalValue,
    int ProductsWithoutCost,
    int ProductsWithNegativeStock,
    int ValidationErrors,
    DateOnly EndDate);

/// <summary>The generated inventory file, on its way to the browser.</summary>
public sealed record InventoryFile(
    string FileName,
    byte[] Content,
    int ProductsWithoutCost,
    int ValidationErrors);

/// <param name="Scope">Total, Warehouse or Products.</param>
/// <param name="Status">Open or Closed.</param>
public sealed record InventoryCount(
    Guid Id,
    Guid CompanyId,
    Guid? WarehouseId,
    string Reference,
    string Scope,
    string Status,
    DateOnly CountDate,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    int LineCount,
    int LinesWithDifference,
    IReadOnlyList<InventoryCountLine> Lines)
{
    public bool IsOpen => Status == "Open";
}

/// <param name="AppliedDifference">What was written to the ledger on closing; null while open.</param>
public sealed record InventoryCountLine(
    Guid Id,
    Guid WarehouseId,
    string ProductCode,
    string ProductDescription,
    decimal SystemQuantity,
    decimal CountedQuantity,
    decimal? AppliedDifference)
{
    /// <summary>What closing the count would write to the ledger for this line.</summary>
    public decimal Difference => CountedQuantity - SystemQuantity;
}

/// <param name="StartAtZero">
/// Opens every line at zero. Closing such a count empties the stock in scope — the zeroing step
/// before recounting from nothing.
/// </param>
public sealed record OpenInventoryCountRequest(
    Guid CompanyId,
    string Reference,
    DateOnly CountDate,
    Guid? WarehouseId = null,
    IReadOnlyList<string>? ProductCodes = null,
    bool StartAtZero = false);

public sealed record CountedLineRequest(Guid LineId, decimal CountedQuantity);

/// <summary>What a document series does to stock, for the UI selects.</summary>
public static class StockEffects
{
    public static readonly (string Code, string Label)[] All =
    [
        ("None", "Não movimenta"),
        ("In", "Entrada"),
        ("Out", "Saída")
    ];

    public static string Describe(string code) =>
        All.FirstOrDefault(effect => effect.Code == code).Label ?? code;
}
