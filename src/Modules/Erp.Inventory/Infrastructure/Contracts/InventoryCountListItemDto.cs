namespace Erp.Inventory.Infrastructure.Contracts;

/// <summary>
/// A row of the inventory count list. Carries no lines: the grid only shows how many there are and
/// how many differ, and both are counted by the database.
/// </summary>
public sealed record InventoryCountListItemDto
{
    public Guid Id { get; init; }

    public Guid? WarehouseId { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string Scope { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateOnly CountDate { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? ClosedAtUtc { get; init; }

    public int LineCount { get; init; }

    public int LinesWithDifference { get; init; }

    public bool IsOpen { get; init; }
}
