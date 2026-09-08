using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Purchasing.Storage.Storage;

public sealed class SupplierReturnStorage(ErpDbContext dbContext) : ISupplierReturnStorage
{
    public async Task<IReadOnlyList<SupplierReturn>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SupplierReturn>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .OrderByDescending(x => x.ReturnDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<SupplierReturn?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<SupplierReturn>()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetReturnedQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiptLineIds);

        if (receiptLineIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var rows = await dbContext.Set<SupplierReturnLine>()
            .AsNoTracking()
            .Where(line => receiptLineIds.Contains(line.ReceiptLineId))
            // A voided return sent nothing back, so what it took goes back on the shelf.
            .Where(line => line.Return.Status == SupplierReturnStatus.Returned)
            .GroupBy(line => line.ReceiptLineId)
            .Select(group => new { ReceiptLineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ReceiptLineId, row => row.Quantity);
    }

    public async Task<int> GetLastSequenceAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"DEV{year}/";

        var numbers = await dbContext.Set<SupplierReturn>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Number.StartsWith(prefix))
            .Select(x => x.Number)
            .ToListAsync(cancellationToken);

        return numbers
            .Select(number => int.TryParse(number[prefix.Length..], out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    public Task<bool> NumberExistsAsync(Guid companyId, string number, CancellationToken cancellationToken = default) =>
        dbContext.Set<SupplierReturn>().AnyAsync(x => x.CompanyId == companyId && x.Number == number, cancellationToken);

    public async Task AddAsync(SupplierReturn supplierReturn, CancellationToken cancellationToken = default) =>
        await dbContext.Set<SupplierReturn>().AddAsync(supplierReturn, cancellationToken);
}
