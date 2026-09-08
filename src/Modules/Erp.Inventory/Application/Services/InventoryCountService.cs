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

        if (lines.Count == 0)
            throw new ArgumentException("There is no stock in the chosen scope to count.", nameof(request));

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
                userId);

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
                    line.AppliedDifference))]);
}
