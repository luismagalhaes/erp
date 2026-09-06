using Erp.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Erp.Core.Storage.Data;

public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<UserCompany> UserCompanies => Set<UserCompany>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.Property(x => x.TaxId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(30);
            entity.HasIndex(x => x.TaxId).IsUnique();
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
    }
}
