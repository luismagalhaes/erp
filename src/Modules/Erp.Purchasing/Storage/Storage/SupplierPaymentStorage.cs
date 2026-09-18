using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Storage;

public sealed class SupplierPaymentStorage(AppDbContext dbContext) : ISupplierPaymentStorage
{
    public IQueryable<SupplierPaymentListItemDto> Query(Guid companyId) =>
        dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new SupplierPaymentListItemDto
            {
                Id = x.Id,
                Number = x.Number,
                PaymentDate = x.PaymentDate,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier.Name,
                SupplierTaxId = x.Supplier.TaxId,
                Total = x.Total,
                SettledDocumentCount = x.Lines.Count,
                Status = x.Status == SupplierPaymentStatus.Voided ? "Voided" : "Recorded",
                IsVoided = x.Status == SupplierPaymentStatus.Voided
            });

    public async Task<IReadOnlyList<SupplierPayment>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<SupplierPayment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<SupplierPayment>()
            .Include(x => x.Lines)
            .Include(x => x.Methods)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPaidAmountsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        if (documentIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        // A voided payment pays nothing, so the documents it touched go back to being owed.
        var rows = await dbContext.Set<SupplierPaymentLine>()
            .AsNoTracking()
            .Where(line => documentIds.Contains(line.DocumentId))
            .Where(line => line.Payment.Status == SupplierPaymentStatus.Recorded)
            .GroupBy(line => line.DocumentId)
            .Select(group => new { DocumentId = group.Key, Amount = group.Sum(line => line.AppliedAmount) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.DocumentId, row => row.Amount);
    }

    public async Task AddAsync(SupplierPayment payment, CancellationToken cancellationToken = default) =>
        await dbContext.Set<SupplierPayment>().AddAsync(payment, cancellationToken);
}
