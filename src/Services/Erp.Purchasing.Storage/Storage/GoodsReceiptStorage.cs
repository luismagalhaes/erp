using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Storage;

public sealed class GoodsReceiptStorage(PurchasingDbContext dbContext) : IGoodsReceiptStorage
{
    public async Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .OrderByDescending(x => x.ReceiptDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.GoodsReceipts
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<GoodsReceipt?> GetForUpdateByLineAsync(
        Guid receiptLineId,
        CancellationToken cancellationToken = default)
    {
        var receiptId = await dbContext.GoodsReceiptLines
            .AsNoTracking()
            .Where(x => x.Id == receiptLineId)
            .Select(x => (Guid?)x.ReceiptId)
            .FirstOrDefaultAsync(cancellationToken);

        if (receiptId is null)
            return null;

        // EF1002/EF1003: only the table name is interpolated, and it comes from the model. The id
        // is passed as parameter {0}.
#pragma warning disable EF1002, EF1003
        _ = await dbContext.GoodsReceipts
            .FromSqlRaw(
                $"SELECT * FROM {QualifiedTableName.For<GoodsReceipt>(dbContext)} WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {{0}}",
                receiptId.Value)
            .FirstOrDefaultAsync(cancellationToken);
#pragma warning restore EF1002, EF1003

        return await GetByIdAsync(receiptId.Value, cancellationToken);
    }

    public async Task<int> GetLastSequenceAsync(Guid companyId, int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"REC{year}/";

        var numbers = await dbContext.GoodsReceipts
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
        dbContext.GoodsReceipts.AnyAsync(x => x.CompanyId == companyId && x.Number == number, cancellationToken);

    public async Task AddAsync(GoodsReceipt receipt, CancellationToken cancellationToken = default) =>
        await dbContext.GoodsReceipts.AddAsync(receipt, cancellationToken);
}
