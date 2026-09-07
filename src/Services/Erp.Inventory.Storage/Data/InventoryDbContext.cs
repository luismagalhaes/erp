using Erp.Inventory.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Inventory.Storage.Data;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountLine> InventoryCountLines => Set<InventoryCountLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockLedgerEntry>(entity =>
        {
            entity.ToTable("StockLedgerEntry");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Direction).HasConversion<byte>();
            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitCost).HasPrecision(19, 6);
            entity.Property(x => x.SourceDocumentType).HasMaxLength(4);
            entity.Property(x => x.SourceDocumentNumber).HasMaxLength(60);
            entity.Property(x => x.Reason).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);

            entity.HasIndex(x => new { x.CompanyId, x.WarehouseId, x.ProductCode });
            entity.HasIndex(x => new { x.CompanyId, x.MovementDate });

            // A document line moves stock once and once only. The rule lives in the service, but
            // the database refuses the second entry even if the rule were ever to fail.
            entity.HasIndex(x => x.SourceLineId)
                .IsUnique()
                .HasFilter("[SourceLineId] IS NOT NULL")
                .HasDatabaseName("IX_StockLedgerEntry_SourceLineId");
        });

        modelBuilder.Entity<InventoryCount>(entity =>
        {
            entity.ToTable("InventoryCount");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Reference).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Scope).HasConversion<byte>();
            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.ClosedByUserId).HasMaxLength(450);

            entity.Ignore(x => x.IsOpen);

            entity.HasIndex(x => new { x.CompanyId, x.CountDate });

            // At most one open count per company: two would each close against balances the other
            // moved, and the second would undo the first.
            entity.HasIndex(x => x.CompanyId)
                .IsUnique()
                .HasFilter("[Status] = 1")
                .HasDatabaseName("IX_InventoryCount_CompanyId_Open");

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Count)
                .HasForeignKey(x => x.CountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InventoryCountLine>(entity =>
        {
            entity.ToTable("InventoryCountLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.SystemQuantity).HasPrecision(19, 6);
            entity.Property(x => x.CountedQuantity).HasPrecision(19, 6);
            entity.Property(x => x.AppliedDifference).HasPrecision(19, 6);

            entity.HasIndex(x => new { x.CountId, x.WarehouseId, x.ProductCode }).IsUnique();
        });

        modelBuilder.Entity<StockBalance>(entity =>
        {
            entity.ToTable("StockBalance");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.RowVersion).IsRowVersion();

            entity.HasIndex(x => new { x.CompanyId, x.WarehouseId, x.ProductCode }).IsUnique();
        });
    }
}
