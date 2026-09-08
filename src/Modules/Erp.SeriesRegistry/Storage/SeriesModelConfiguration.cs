using Erp.Storage;
using Microsoft.EntityFrameworkCore;
using Erp.SeriesRegistry.Domain;

namespace Erp.SeriesRegistry.Storage;

/// <summary>The series registry's table, declared by the module that owns it.</summary>
public sealed class SeriesModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<Domain.Series>(entity =>
        {
            entity.ToTable("Series");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DocumentType).HasMaxLength(4).IsRequired();
            entity.Property(x => x.SeriesCode).HasMaxLength(35).IsRequired();
            entity.Property(x => x.EstablishmentCode).HasMaxLength(20);
            entity.Property(x => x.ValidationCode).HasMaxLength(16);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);

            entity.Property(x => x.Status).HasConversion<byte>();
            entity.Property(x => x.StockEffect).HasConversion<byte>();
            entity.Property(x => x.RowVersion).IsRowVersion();

            // Computed from the state, so it stays out of the table.
            entity.Ignore(x => x.CanIssue);

            entity.HasIndex(x => new { x.CompanyId, x.DocumentType, x.SeriesCode }).IsUnique();
            entity.HasIndex(x => x.CompanyId);
        });
    }
}
