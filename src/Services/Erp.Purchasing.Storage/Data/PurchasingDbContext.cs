using Erp.Purchasing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Purchasing.Storage.Data;

public sealed class PurchasingDbContext(DbContextOptions<PurchasingDbContext> options) : DbContext(options)
{
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceLine> PurchaseInvoiceLines => Set<PurchaseInvoiceLine>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierReturnLine> SupplierReturnLines => Set<SupplierReturnLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrder");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Number).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.ClosedReason).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);

            entity.Property(x => x.NetTotal).HasPrecision(19, 6);
            entity.Property(x => x.TaxTotal).HasPrecision(19, 6);
            entity.Property(x => x.GrossTotal).HasPrecision(19, 6);

            entity.Property(x => x.RowVersion).IsRowVersion();

            // Our own number, but still one per company: two orders called the same would make the
            // supplier's reply ambiguous.
            entity.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.Status });
            entity.HasIndex(x => new { x.CompanyId, x.SupplierId });

            // Flattened into the order's own table, like the customer on a sales document: a
            // snapshot is part of the row it belongs to, not a thing of its own.
            entity.OwnsOne(x => x.Supplier, supplier =>
            {
                supplier.Property(x => x.Code).HasColumnName("SupplierCode").HasMaxLength(60).IsRequired();
                supplier.Property(x => x.Name).HasColumnName("SupplierName").HasMaxLength(200).IsRequired();
                supplier.Property(x => x.TaxId).HasColumnName("SupplierTaxId").HasMaxLength(30).IsRequired();
                supplier.Property(x => x.Address).HasColumnName("SupplierAddress").HasMaxLength(400);
                supplier.Property(x => x.PostalCode).HasColumnName("SupplierPostalCode").HasMaxLength(20);
                supplier.Property(x => x.City).HasColumnName("SupplierCity").HasMaxLength(100);
                supplier.Property(x => x.Country).HasColumnName("SupplierCountry").HasMaxLength(2).IsRequired();
            });

            entity.Navigation(x => x.Supplier).IsRequired();

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.ToTable("PurchaseOrderLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitPrice).HasPrecision(19, 6);
            entity.Property(x => x.LineAmount).HasPrecision(19, 6);
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 6);
            entity.Property(x => x.ReceivedQuantity).HasPrecision(19, 6);

            // Computed from stored columns, so it stays out of the table.
            entity.Ignore(x => x.PendingQuantity);
            entity.Ignore(x => x.IsFullyReceived);

            entity.HasIndex(x => new { x.OrderId, x.LineNumber }).IsUnique();
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.ToTable("GoodsReceipt");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Number).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.SupplierDocumentNumber).HasMaxLength(60);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.VoidReason).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.VoidedByUserId).HasMaxLength(450);

            entity.Property(x => x.TotalCost).HasPrecision(19, 6);

            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.SupplierId });
            entity.HasIndex(x => new { x.CompanyId, x.ReceiptDate });

            entity.OwnsOne(x => x.Supplier, supplier =>
            {
                supplier.Property(x => x.Code).HasColumnName("SupplierCode").HasMaxLength(60).IsRequired();
                supplier.Property(x => x.Name).HasColumnName("SupplierName").HasMaxLength(200).IsRequired();
                supplier.Property(x => x.TaxId).HasColumnName("SupplierTaxId").HasMaxLength(30).IsRequired();
                supplier.Property(x => x.Address).HasColumnName("SupplierAddress").HasMaxLength(400);
                supplier.Property(x => x.PostalCode).HasColumnName("SupplierPostalCode").HasMaxLength(20);
                supplier.Property(x => x.City).HasColumnName("SupplierCity").HasMaxLength(100);
                supplier.Property(x => x.Country).HasColumnName("SupplierCountry").HasMaxLength(2).IsRequired();
            });

            entity.Navigation(x => x.Supplier).IsRequired();

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Receipt)
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.ToTable("GoodsReceiptLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitCost).HasPrecision(19, 6);
            entity.Property(x => x.LineAmount).HasPrecision(19, 6);

            entity.HasIndex(x => new { x.ReceiptId, x.LineNumber }).IsUnique();

            // How much of an order line is received is read through these, so the lookup is worth
            // an index. No foreign key: the order line may be gone while the receipt stays.
            entity.HasIndex(x => x.OrderLineId);
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.ToTable("PurchaseInvoice");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DocumentType).HasMaxLength(4).IsRequired();
            entity.Property(x => x.SupplierDocumentNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.SupplierAtcud).HasMaxLength(60);
            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.VoidReason).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.VoidedByUserId).HasMaxLength(450);

            entity.Property(x => x.NetTotal).HasPrecision(19, 6);
            entity.Property(x => x.TaxTotal).HasPrecision(19, 6);
            entity.Property(x => x.GrossTotal).HasPrecision(19, 6);

            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.CompanyId, x.SupplierId });
            entity.HasIndex(x => new { x.CompanyId, x.SupplierDocumentDate });

            entity.OwnsOne(x => x.Supplier, supplier =>
            {
                supplier.Property(x => x.Code).HasColumnName("SupplierCode").HasMaxLength(60).IsRequired();
                supplier.Property(x => x.Name).HasColumnName("SupplierName").HasMaxLength(200).IsRequired();
                supplier.Property(x => x.TaxId).HasColumnName("SupplierTaxId").HasMaxLength(30).IsRequired();
                supplier.Property(x => x.Address).HasColumnName("SupplierAddress").HasMaxLength(400);
                supplier.Property(x => x.PostalCode).HasColumnName("SupplierPostalCode").HasMaxLength(20);
                supplier.Property(x => x.City).HasColumnName("SupplierCity").HasMaxLength(100);
                supplier.Property(x => x.Country).HasColumnName("SupplierCountry").HasMaxLength(2).IsRequired();

                supplier.HasIndex(x => x.TaxId);
            });

            entity.Navigation(x => x.Supplier).IsRequired();

            // The unique index that makes double-recording impossible —
            // (CompanyId, SupplierTaxId, SupplierDocumentNumber) — is created in the migration, not
            // here: it spans a column of the owner and one of the owned supplier snapshot, and the
            // model builder has no way to say that. Both are columns of this same table, so the
            // database enforces it all the same.

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Invoice)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.TaxSummary)
                .WithOne(x => x.Invoice)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseInvoiceLine>(entity =>
        {
            entity.ToTable("PurchaseInvoiceLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.DeductionNature).HasConversion<byte>();

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitPrice).HasPrecision(19, 6);
            entity.Property(x => x.LineAmount).HasPrecision(19, 6);
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 6);

            entity.Ignore(x => x.MovesStock);

            entity.HasIndex(x => new { x.InvoiceId, x.LineNumber }).IsUnique();

            // How much of a receipt line is already invoiced is read through this.
            entity.HasIndex(x => x.ReceiptLineId);
            entity.HasIndex(x => x.ReturnLineId);
        });

        modelBuilder.Entity<SupplierReturn>(entity =>
        {
            entity.ToTable("SupplierReturn");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Number).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.Reason).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.VoidReason).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.VoidedByUserId).HasMaxLength(450);

            entity.Property(x => x.TotalCost).HasPrecision(19, 6);

            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.CompanyId, x.Number }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.SupplierId });

            entity.OwnsOne(x => x.Supplier, supplier =>
            {
                supplier.Property(x => x.Code).HasColumnName("SupplierCode").HasMaxLength(60).IsRequired();
                supplier.Property(x => x.Name).HasColumnName("SupplierName").HasMaxLength(200).IsRequired();
                supplier.Property(x => x.TaxId).HasColumnName("SupplierTaxId").HasMaxLength(30).IsRequired();
                supplier.Property(x => x.Address).HasColumnName("SupplierAddress").HasMaxLength(400);
                supplier.Property(x => x.PostalCode).HasColumnName("SupplierPostalCode").HasMaxLength(20);
                supplier.Property(x => x.City).HasColumnName("SupplierCity").HasMaxLength(100);
                supplier.Property(x => x.Country).HasColumnName("SupplierCountry").HasMaxLength(2).IsRequired();
            });

            entity.Navigation(x => x.Supplier).IsRequired();

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Return)
                .HasForeignKey(x => x.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierReturnLine>(entity =>
        {
            entity.ToTable("SupplierReturnLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitCost).HasPrecision(19, 6);
            entity.Property(x => x.LineAmount).HasPrecision(19, 6);

            entity.HasIndex(x => new { x.ReturnId, x.LineNumber }).IsUnique();

            // How much of a receipt line has gone back is read through this.
            entity.HasIndex(x => x.ReceiptLineId);
        });

        modelBuilder.Entity<PurchaseInvoiceTaxSummary>(entity =>
        {
            entity.ToTable("PurchaseInvoiceTaxSummary");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxableBase).HasPrecision(19, 6);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 6);

            entity.HasIndex(x => x.InvoiceId);
        });
    }
}
