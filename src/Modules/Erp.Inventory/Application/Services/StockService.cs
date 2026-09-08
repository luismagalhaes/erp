using Erp.Common;
using Erp.Inventory.Domain;
using Erp.Inventory.Infrastructure.Application;
using Erp.Inventory.Infrastructure.Contracts;
using Erp.Inventory.Infrastructure.Storage;

namespace Erp.Inventory.Application.Services;

public sealed class StockService(IStockStorage storage, IUnitOfWork unitOfWork) : IStockService
{
    public async Task<IReadOnlyList<StockBalanceDto>> GetBalancesAsync(
        Guid companyId,
        Guid? warehouseId = null,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var balances = await storage.GetBalancesAsync(companyId, warehouseId, productCode, cancellationToken);

        return balances.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<StockLedgerEntryDto>> GetLedgerAsync(
        Guid companyId,
        Guid warehouseId,
        string productCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);

        var entries = await storage.GetEntriesAsync(companyId, warehouseId, productCode, cancellationToken);

        var running = 0m;
        var result = new List<StockLedgerEntryDto>(entries.Count);

        // Oldest first, accumulating: read down the column and you see how the balance got there.
        foreach (var entry in entries.OrderBy(x => x.SystemEntryDateUtc).ThenBy(x => x.Id))
        {
            running += entry.Quantity;

            result.Add(new StockLedgerEntryDto(
                entry.Id,
                entry.WarehouseId,
                entry.ProductCode,
                entry.Direction.ToString(),
                entry.Quantity,
                running,
                entry.MovementDate,
                entry.SystemEntryDateUtc,
                entry.SourceDocumentType,
                entry.SourceDocumentNumber,
                entry.Reason));
        }

        return result;
    }

    public async Task<StockCheckResultDto> CheckAsync(
        Guid companyId,
        string? productCode = null,
        CancellationToken cancellationToken = default)
    {
        var balances = await storage.GetBalancesAsync(companyId, productCode: productCode, cancellationToken: cancellationToken);

        // Recomputed from the ledger, never from the balances — checking a projection against
        // itself would agree with anything.
        var movements = await storage.GetMovementsAsync(companyId, productCode: productCode, cancellationToken: cancellationToken);
        var ledger = StockValuation.Replay(movements)
            .ToDictionary(line => (line.WarehouseId, line.ProductCode));

        var lines = new List<StockCheckLineDto>();

        foreach (var balance in balances)
        {
            var key = (balance.WarehouseId, balance.ProductCode);
            ledger.TryGetValue(key, out var recomputed);

            var quantity = recomputed?.Quantity ?? 0m;
            var value = recomputed?.Value ?? 0m;

            lines.Add(new StockCheckLineDto(
                balance.WarehouseId,
                balance.ProductCode,
                balance.ProductDescription,
                balance.Quantity,
                quantity,
                balance.Quantity - quantity,
                balance.StockValue,
                value,
                balance.StockValue - value,
                recomputed?.MovementCount ?? 0));
        }

        // A product with entries but no balance row at all is the worse kind of failure: stock
        // moved and nothing recorded it. Reported with a recorded quantity of zero.
        var known = balances.Select(x => (x.WarehouseId, x.ProductCode)).ToHashSet();

        foreach (var orphan in ledger.Where(entry => !known.Contains(entry.Key)).Select(entry => entry.Value))
        {
            lines.Add(new StockCheckLineDto(
                orphan.WarehouseId,
                orphan.ProductCode,
                orphan.ProductDescription,
                0m,
                orphan.Quantity,
                -orphan.Quantity,
                0m,
                orphan.Value,
                -orphan.Value,
                orphan.MovementCount));
        }

        return new StockCheckResultDto(
            companyId,
            DateTime.UtcNow,
            lines.Count,
            lines.Count(line => line.Difference != 0),
            lines.Count(line => line.ValueDifference != 0),
            [.. lines
                .OrderByDescending(line => Math.Abs(line.Difference))
                .ThenByDescending(line => Math.Abs(line.ValueDifference))
                .ThenBy(line => line.ProductCode, StringComparer.Ordinal)]);
    }

    public async Task<StockBalanceDto> AdjustAsync(
        AdjustStockRequest request,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProductCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);

        if (request.Difference == 0)
            throw new ArgumentException("An adjustment of zero changes nothing.", nameof(request));

        var entry = StockLedgerEntry.FromAdjustment(
            request.CompanyId,
            request.WarehouseId,
            request.ProductCode.Trim(),
            request.ProductDescription,
            request.Difference,
            request.MovementDate,
            request.Reason.Trim(),
            createdByUserId: userId,
            unitCost: request.UnitCost);

        // The lock on the balance row is only worth taking inside a transaction: a lock taken by a
        // bare SELECT is released the moment that statement ends, which is long before the write
        // that depends on it. Every other path that moves stock is called from inside a transaction
        // its caller opened; a manual adjustment has no such caller, so it opens its own.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        var balance = await ApplyAsync(entry, cancellationToken);

        await storage.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Map(balance);
    }

    private static StockBalanceDto Map(StockBalance balance) =>
        new(balance.WarehouseId,
            balance.ProductCode,
            balance.ProductDescription,
            balance.Quantity,
            balance.AverageCost,
            balance.StockValue,
            balance.LastMovementUtc);

    /// <summary>
    /// Writes the entry and moves the balance with it. The balance row is locked first, so two
    /// movements of the same product cannot read the same figure and both write over it.
    /// </summary>
    private async Task<StockBalance> ApplyAsync(StockLedgerEntry entry, CancellationToken cancellationToken)
    {
        var balance = await storage.GetBalanceForUpdateAsync(
            entry.CompanyId, entry.WarehouseId, entry.ProductCode, cancellationToken);

        if (balance is null)
        {
            balance = StockBalance.Start(entry.CompanyId, entry.WarehouseId, entry.ProductCode, entry.ProductDescription);
            await storage.AddBalanceAsync(balance, cancellationToken);
        }

        balance.Apply(entry);

        await storage.AddEntryAsync(entry, cancellationToken);

        return balance;
    }
}
