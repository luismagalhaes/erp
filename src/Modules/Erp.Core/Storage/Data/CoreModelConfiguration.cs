using Erp.Core.Domain;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Data;

/// <summary>The Core module's tables, declared by the module itself.</summary>
public sealed class CoreModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.Property(x => x.TaxId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.Property(x => x.Address).HasMaxLength(400);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(2).IsRequired();
            entity.HasIndex(x => x.TaxId).IsUnique();
        });

        modelBuilder.Entity<CompanyAtCredential>(entity =>
        {
            entity.HasKey(x => x.CompanyId);
            entity.Property(x => x.SubUserId).HasMaxLength(50).IsRequired();
            entity.Property(x => x.ProtectedPassword).HasMaxLength(1000).IsRequired();

            entity.HasOne(x => x.Company)
                .WithOne()
                .HasForeignKey<CompanyAtCredential>(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCompany>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(450).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();

            entity.HasIndex(x => new { x.UserId, x.CompanyId }).IsUnique();
            entity.HasIndex(x => x.CompanyId);
            entity.HasIndex(x => x.UserId);

            entity.HasOne(x => x.Company)
                .WithMany(x => x.UserCompanies)
                .HasForeignKey(x => x.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Address).HasMaxLength(400);
            entity.Property(x => x.City).HasMaxLength(120);
            entity.Property(x => x.PostalCode).HasMaxLength(20);
            entity.Property(x => x.Country).HasMaxLength(2).IsRequired();

            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();

            // At most one default per company, enforced by the database rather than only by the
            // service: a second default would make "where does the stock go" ambiguous.
            entity.HasIndex(x => x.CompanyId)
                .IsUnique()
                .HasFilter("[IsDefault] = 1")
                .HasDatabaseName("IX_Warehouse_CompanyId_Default");
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ProductFamily>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<ProductSubfamily>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.CompanyId, x.Code }).IsUnique();
            entity.HasIndex(x => x.FamilyId);

            entity.HasOne(x => x.Family)
                .WithMany(x => x.Subfamilies)
                .HasForeignKey(x => x.FamilyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProductType).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.InventoryCategory).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(x => x.DefaultTaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.DefaultTaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Barcode).HasMaxLength(60);

            entity.Property(x => x.UnitPrice).HasPrecision(19, 6);
            entity.Property(x => x.UnitCost).HasPrecision(19, 6);
            entity.Property(x => x.DefaultTaxPercentage).HasPrecision(5, 2);

            entity.HasIndex(x => new { x.CompanyId, x.ProductCode }).IsUnique();

            // Classification is optional, and a family in use can never be deleted from under it.
            entity.HasOne(x => x.Family)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.FamilyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Subfamily)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.SubfamilyId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Brand)
                .WithMany(x => x.Products)
                .HasForeignKey(x => x.BrandId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Customer>(entity => ConfigurePartner(entity));
        modelBuilder.Entity<Supplier>(entity => ConfigurePartner(entity));

        modelBuilder.Entity<VatRate>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FiscalRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Percentage).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.FiscalRegion, x.Code }).IsUnique();
        });
    }

    /// <summary>Customers and suppliers share the same columns, so they share the same mapping.</summary>
    private static void ConfigurePartner<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity)
        where T : class
    {
        entity.HasKey("Id");
        entity.Property("Code").HasMaxLength(30).IsRequired();
        entity.Property("Name").HasMaxLength(200).IsRequired();
        entity.Property("TaxId").HasMaxLength(30).IsRequired();
        entity.Property("Address").HasMaxLength(400);
        entity.Property("PostalCode").HasMaxLength(20);
        entity.Property("City").HasMaxLength(120);
        entity.Property("Country").HasMaxLength(2).IsRequired();
        entity.Property("Email").HasMaxLength(200);
        entity.Property("Phone").HasMaxLength(30);

        entity.HasIndex("CompanyId", "Code").IsUnique();
        entity.HasIndex("CompanyId", "TaxId");
    }
}
