using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;
using Erp.Common;

namespace Erp.Inventory.Application.Services;

public sealed class InventoryCountService(
    IInventoryCountStorage countStorage,
    IStockStorage stockStorage,
    IErpUnitOfWork unitOfWork) : IInventoryCountService
{
    public async Task<IReadOnlyList<InventoryCountDto>> GetAllAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var counts = await countStorage.GetAllAsync(companyId, cancellationToken);
        return counts.Select(Map).ToList();
    }

    public async Task<InventoryCountDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var count = await countStorage.GetByIdAsync(id, cancellationToken);
        return count is null ? null : Map(count);
    }

    public async Task<InventoryCountDto> OpenAsync(
        OpenInventoryCountRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reference);

        // Two open counts over the same stock would each close against balances the other moved.
        if (await countStorage.HasOpenCountAsync(request.CompanyId, cancellationToken))
        {
            throw new InvalidOperationException(
                "There is already an open count for this company. Close it before opening another.");
        }

        var balances = await stockStorage.GetBalancesAsync(
            request.CompanyId, request.WarehouseId, cancellationToken: cancellationToken);

        var wanted = request.ProductCodes is { Count: > 0 }
            ? request.ProductCodes.ToHashSet(StringComparer.Ordinal)
            : null;

        var lines = balances
            .Where(balance => wanted is null || wanted.Contains(balance.ProductCode))
            .Select(balance => new InventoryCountLine
            {
                WarehouseId = balance.WarehouseId,
                ProductCode = balance.ProductCode,
                ProductDescription = balance.ProductDescription,
                SystemQuantity = balance.Quantity
            })
            .ToList();

        // Asked for by name and not on the shelf as far as the system knows — but the caller named
        // it, so it belongs on the sheet at zero rather than being quietly left out.
        if (wanted is not null && request.WarehouseId is { } warehouse)
        {
            var present = lines.Select(line => line.ProductCode).ToHashSet(StringComparer.Ordinal);

            lines.AddRange(wanted
                .Where(code => !present.Contains(code))
                .Select(code => new InventoryCountLine
                {
                    WarehouseId = warehouse,
                    ProductCode = code,
                    // Nothing to take a description from; the product file belongs to another
                    // module, and the code is what the sheet is read by anyway.
                    ProductDescription = code,
                    SystemQuantity = 0m
                }));
        }

        // An empty sheet is not refused. A company that starts with a full warehouse has no
        // balances at all, and a count is how that stock gets in — refusing here would leave the
        // only door locked from the inside. Products are added to the sheet as they are found.

        var scope = DetermineScope(request);

        var count = InventoryCount.Open(
            request.CompanyId,
            request.WarehouseId,
            scope,
            request.Reference,
            request.CountDate,
            lines,
            request.StartAtZero,
            userId);

        await countStorage.AddAsync(count, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(count);
    }

    public async Task<InventoryCountDto?> AddLineAsync(
        Guid countId,
        AddCountLineRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = await countStorage.GetByIdAsync(countId, cancellationToken);
        if (count is null)
            return null;

        var line = count.AddLine(
            request.WarehouseId, request.ProductCode, request.ProductDescription, request.UnitCost);

        await countStorage.AddLineAsync(line, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(count);
    }

    public async Task<InventoryCountDto?> SetCountedAsync(
        Guid countId,
        IReadOnlyList<CountedLineRequest> lines,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var count = await countStorage.GetByIdAsync(countId, cancellationToken);
        if (count is null)
            return null;

        foreach (var line in lines)
            count.SetCounted(line.LineId, line.CountedQuantity);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map(count);
    }

    public async Task<InventoryCountDto?> CloseAsync(
        Guid countId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var count = await countStorage.GetByIdAsync(countId, cancellationToken);
        if (count is null)
            return null;

        // One transaction: the count closing and every adjustment it produces land together, or
        // the warehouse ends up in a state nobody asked for.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // Read now, not from the picture taken at opening: stock may have moved while the count
        // was running, and the adjustment has to land on the balance as it stands.
        var current = new Dictionary<Guid, decimal>(count.Lines.Count);

        foreach (var line in count.Lines)
        {
            var balance = await stockStorage.GetBalanceForUpdateAsync(
                count.CompanyId, line.WarehouseId, line.ProductCode, cancellationToken);

            current[line.Id] = balance?.Quantity ?? 0m;
        }

        var adjustments = count.Close(current, userId, DateTime.UtcNow);

        foreach (var (line, difference) in adjustments)
        {
            var entry = StockLedgerEntry.FromAdjustment(
                count.CompanyId,
                line.WarehouseId,
                line.ProductCode,
                line.ProductDescription,
                difference,
                count.CountDate,
                $"Inventário {count.Reference}",
                count.Id,
                userId,
                // Only a line added to the sheet carries one: what the ledger already knows about
                // is worth the average it already has.
                line.UnitCost);

            await ApplyAsync(entry, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(count);
    }

    /// <summary>Writes the entry and moves the balance with it.</summary>
    private async Task ApplyAsync(StockLedgerEntry entry, CancellationToken cancellationToken)
    {
        var balance = await stockStorage.GetBalanceForUpdateAsync(
            entry.CompanyId, entry.WarehouseId, entry.ProductCode, cancellationToken);

        if (balance is null)
        {
            balance = StockBalance.Start(entry.CompanyId, entry.WarehouseId, entry.ProductCode, entry.ProductDescription);
            await stockStorage.AddBalanceAsync(balance, cancellationToken);
        }

        balance.Apply(entry);

        await stockStorage.AddEntryAsync(entry, cancellationToken);
    }

    private static InventoryCountScope DetermineScope(OpenInventoryCountRequest request)
    {
        if (request.ProductCodes is { Count: > 0 })
            return InventoryCountScope.Products;

        return request.WarehouseId is null ? InventoryCountScope.Total : InventoryCountScope.Warehouse;
    }

    private static InventoryCountDto Map(InventoryCount count) =>
        new(count.Id,
            count.CompanyId,
            count.WarehouseId,
            count.Reference,
            count.Scope.ToString(),
            count.Status.ToString(),
            count.CountDate,
            count.CreatedAtUtc,
            count.ClosedAtUtc,
            count.Lines.Count,
            count.Lines.Count(line => line.CountedQuantity != line.SystemQuantity),
            [.. count.Lines
                .OrderBy(line => line.ProductCode, StringComparer.Ordinal)
                .Select(line => new InventoryCountLineDto(
                    line.Id,
                    line.WarehouseId,
                    line.ProductCode,
                    line.ProductDescription,
                    line.SystemQuantity,
                    line.CountedQuantity,
                    line.AppliedDifference,
                    line.UnitCost))]);
}
