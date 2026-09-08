using Erp.Purchasing.Domain;
using Erp.Purchasing.Infrastructure.Contracts;
using Erp.Purchasing.Infrastructure.Storage;
using Erp.Purchasing.Storage.Data;
using Microsoft.EntityFrameworkCore;
using Erp.Storage;

namespace Erp.Purchasing.Storage.Storage;

public sealed class PurchaseInvoiceStorage(AppDbContext dbContext) : IPurchaseInvoiceStorage
{
    public IQueryable<PurchaseInvoiceListItemDto> Query(Guid companyId) =>
        dbContext.Set<PurchaseInvoice>()
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => new PurchaseInvoiceListItemDto
            {
                Id = x.Id,
                DocumentType = x.DocumentType,
                SupplierDocumentNumber = x.SupplierDocumentNumber,
                SupplierDocumentDate = x.SupplierDocumentDate,
                ReceivedDate = x.ReceivedDate,
                DueDate = x.DueDate,
                SupplierName = x.Supplier.Name,
                SupplierTaxId = x.Supplier.TaxId,
                NetTotal = x.NetTotal,
                TaxTotal = x.TaxTotal,
                GrossTotal = x.GrossTotal,
                ReverseCharge = x.ReverseCharge,
                Status = x.Status == PurchaseInvoiceStatus.Voided ? "Voided" : "Recorded",
                IsVoided = x.Status == PurchaseInvoiceStatus.Voided
            });

    public async Task<IReadOnlyList<PurchaseInvoice>> GetAllAsync(
        Guid companyId,
        Guid? supplierId = null,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<PurchaseInvoice>()
            .AsNoTracking()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummary)
            .Where(x => x.CompanyId == companyId)
            .Where(x => supplierId == null || x.SupplierId == supplierId)
            .OrderByDescending(x => x.SupplierDocumentDate)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<PurchaseInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<PurchaseInvoice>()
            .Include(x => x.Lines)
            .Include(x => x.TaxSummary)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(
        Guid companyId,
        string supplierTaxId,
        string supplierDocumentNumber,
        CancellationToken cancellationToken = default) =>
        dbContext.Set<PurchaseInvoice>().AnyAsync(
            x => x.CompanyId == companyId
                 && x.Supplier.TaxId == supplierTaxId
                 && x.SupplierDocumentNumber == supplierDocumentNumber,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetInvoicedQuantitiesAsync(
        IReadOnlyCollection<Guid> receiptLineIds,
        Guid? excludeInvoiceId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiptLineIds);

        if (receiptLineIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var rows = await dbContext.Set<PurchaseInvoiceLine>()
            .AsNoTracking()
            .Where(line => line.ReceiptLineId != null && receiptLineIds.Contains(line.ReceiptLineId.Value))
            // A voided invoice invoices nothing, so what it took goes back on the shelf.
            .Where(line => line.Invoice.Status == PurchaseInvoiceStatus.Recorded)
            .Where(line => excludeInvoiceId == null || line.InvoiceId != excludeInvoiceId)
            .GroupBy(line => line.ReceiptLineId!.Value)
            .Select(group => new { ReceiptLineId = group.Key, Quantity = group.Sum(line => line.Quantity) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ReceiptLineId, row => row.Quantity);
    }

    public async Task AddAsync(PurchaseInvoice invoice, CancellationToken cancellationToken = default) =>
        await dbContext.Set<PurchaseInvoice>().AddAsync(invoice, cancellationToken);
}
