using Erp.Sales.Domain;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Sales.Storage.Data;

/// <summary>The Sales module's tables, declared by the module itself.</summary>
public sealed class SalesModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<SalesDocument>(entity =>
        {
            entity.ToTable("SalesDocument");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.DocumentType).HasMaxLength(4).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Atcud).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.SourceBilling).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.CustomerTaxId).HasMaxLength(30).IsRequired();
            entity.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CustomerAddress).HasMaxLength(400);
            entity.Property(x => x.CustomerPostalCode).HasMaxLength(20);
            entity.Property(x => x.CustomerCity).HasMaxLength(100);
            entity.Property(x => x.CustomerCountry).HasMaxLength(2).IsRequired();
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.RectifiedDocumentNumber).HasMaxLength(60);
            entity.Property(x => x.RectificationReason).HasMaxLength(400);

            entity.Property(x => x.NetTotal).HasPrecision(19, 2);
            entity.Property(x => x.TaxPayable).HasPrecision(19, 2);
            entity.Property(x => x.GrossTotal).HasPrecision(19, 2);

            entity.Property(x => x.Hash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PreviousHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HashControl).HasMaxLength(70).IsRequired();
            entity.Property(x => x.QrCodePayload).HasMaxLength(1024);

            entity.Ignore(x => x.EffectiveStatus);
            entity.Ignore(x => x.IsVoided);
            entity.Ignore(x => x.IsRectifying);
            entity.Ignore(x => x.GrossLinesTotal);
            entity.Ignore(x => x.DiscountTotal);

            // Numbering has no gaps and no repeats inside a series.
            entity.HasIndex(x => new { x.SeriesId, x.SequenceNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.DocumentNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.DocumentDate });

            entity.HasMany(x => x.Payments)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Series)
                .WithMany()
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SalesDocument>()
                .WithMany()
                .HasForeignKey(x => x.RectifiedDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.TaxSummaries)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.StatusChanges)
                .WithOne(x => x.Document)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesDocumentLine>(entity =>
        {
            entity.ToTable("SalesDocumentLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.TaxExemptionCode).HasMaxLength(10);
            entity.Property(x => x.TaxExemptionReason).HasMaxLength(200);
            entity.Property(x => x.OriginatingNumber).HasMaxLength(60);

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitPrice).HasPrecision(19, 6);
            entity.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(19, 2);
            entity.Property(x => x.LineAmount).HasPrecision(19, 2);
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 2);

            entity.Ignore(x => x.GrossAmount);
            entity.Ignore(x => x.NetUnitPrice);

            entity.HasIndex(x => new { x.DocumentId, x.LineNumber }).IsUnique();

            // What a movement line still has left to invoice is read through this column.
            entity.HasIndex(x => x.OriginatingLineId);

            entity.HasOne<StockMovementLine>()
                .WithMany()
                .HasForeignKey(x => x.OriginatingLineId)
                .OnDelete(DeleteBehavior.Restrict);

            // Which article line an eco-fee line belongs to.
            entity.HasIndex(x => x.EcoFeeForLineId);

            entity.HasOne<SalesDocumentLine>()
                .WithMany()
                .HasForeignKey(x => x.EcoFeeForLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentTaxSummary>(entity =>
        {
            entity.ToTable("DocumentTaxSummary");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxableBase).HasPrecision(19, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 2);

            entity.HasIndex(x => x.DocumentId);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("StockMovement");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.MovementType).HasMaxLength(2).IsRequired();
            entity.Property(x => x.DocumentNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Atcud).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.SourceBilling).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.PartyTaxId).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PartyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.VehiclePlate).HasMaxLength(20);
            entity.Property(x => x.Comments).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
            entity.Property(x => x.AtDocCodeId).HasMaxLength(60);

            entity.Property(x => x.TotalQuantity).HasPrecision(19, 6);
            entity.Property(x => x.NetTotal).HasPrecision(19, 2);
            entity.Property(x => x.TaxPayable).HasPrecision(19, 2);
            entity.Property(x => x.GrossTotal).HasPrecision(19, 2);

            entity.Property(x => x.Hash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PreviousHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HashControl).HasMaxLength(70).IsRequired();
            entity.Property(x => x.QrCodePayload).HasMaxLength(1024);

            entity.Ignore(x => x.EffectiveStatus);
            entity.Ignore(x => x.IsVoided);

            // Loading and delivery points travel with the document, like every other snapshot.
            entity.ComplexProperty(x => x.ShipFrom, ship =>
            {
                ship.Property(x => x.Address).HasMaxLength(400).HasColumnName("ShipFromAddress").IsRequired();
                ship.Property(x => x.City).HasMaxLength(120).HasColumnName("ShipFromCity");
                ship.Property(x => x.PostalCode).HasMaxLength(20).HasColumnName("ShipFromPostalCode");
                ship.Property(x => x.Country).HasMaxLength(2).HasColumnName("ShipFromCountry").IsRequired();
                ship.Property(x => x.WarehouseId).HasMaxLength(50).HasColumnName("ShipFromWarehouseId");
                ship.Property(x => x.LocationId).HasMaxLength(50).HasColumnName("ShipFromLocationId");
            });

            entity.ComplexProperty(x => x.ShipTo, ship =>
            {
                ship.Property(x => x.Address).HasMaxLength(400).HasColumnName("ShipToAddress").IsRequired();
                ship.Property(x => x.City).HasMaxLength(120).HasColumnName("ShipToCity");
                ship.Property(x => x.PostalCode).HasMaxLength(20).HasColumnName("ShipToPostalCode");
                ship.Property(x => x.Country).HasMaxLength(2).HasColumnName("ShipToCountry").IsRequired();
                ship.Property(x => x.WarehouseId).HasMaxLength(50).HasColumnName("ShipToWarehouseId");
                ship.Property(x => x.LocationId).HasMaxLength(50).HasColumnName("ShipToLocationId");
            });

            // Numbering has no gaps and no repeats inside a series.
            entity.HasIndex(x => new { x.SeriesId, x.SequenceNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.DocumentNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.MovementDate });

            entity.HasOne(x => x.Series)
                .WithMany()
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.StatusChanges)
                .WithOne(x => x.Movement)
                .HasForeignKey(x => x.MovementId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovementLine>(entity =>
        {
            entity.ToTable("StockMovementLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ProductCode).HasMaxLength(60).IsRequired();
            entity.Property(x => x.ProductDescription).HasMaxLength(200).IsRequired();
            entity.Property(x => x.UnitOfMeasure).HasMaxLength(20).IsRequired();
            entity.Property(x => x.TaxCountryRegion).HasMaxLength(5).IsRequired();
            entity.Property(x => x.TaxCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.TaxExemptionCode).HasMaxLength(10);
            entity.Property(x => x.TaxExemptionReason).HasMaxLength(200);

            entity.Property(x => x.Quantity).HasPrecision(19, 6);
            entity.Property(x => x.UnitPrice).HasPrecision(19, 6);
            entity.Property(x => x.DiscountPercentage).HasPrecision(5, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(19, 2);
            entity.Property(x => x.LineAmount).HasPrecision(19, 2);
            entity.Property(x => x.TaxPercentage).HasPrecision(5, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(19, 2);

            entity.Ignore(x => x.GrossAmount);
            entity.Ignore(x => x.NetUnitPrice);

            entity.HasIndex(x => new { x.MovementId, x.LineNumber }).IsUnique();

            // Which article line an eco-fee line belongs to.
            entity.HasIndex(x => x.EcoFeeForLineId);

            entity.HasOne<StockMovementLine>()
                .WithMany()
                .HasForeignKey(x => x.EcoFeeForLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovementStatusChange>(entity =>
        {
            entity.ToTable("MovementStatusChange");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PreviousStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.NewStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(400).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(450);

            entity.HasIndex(x => x.MovementId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PaymentType).HasMaxLength(2).IsRequired();
            entity.Property(x => x.PaymentRefNo).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Atcud).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.SourcePayment).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.PartyTaxId).HasMaxLength(30).IsRequired();
            entity.Property(x => x.PartyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(400);
            entity.Property(x => x.CreatedByUserId).HasMaxLength(450);

            entity.Property(x => x.NetTotal).HasPrecision(19, 2);
            entity.Property(x => x.TaxPayable).HasPrecision(19, 2);
            entity.Property(x => x.GrossTotal).HasPrecision(19, 2);

            entity.Property(x => x.Hash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.PreviousHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HashControl).HasMaxLength(70).IsRequired();
            entity.Property(x => x.QrCodePayload).HasMaxLength(1024);

            entity.Ignore(x => x.EffectiveStatus);
            entity.Ignore(x => x.IsVoided);

            // Numbering has no gaps and no repeats inside a series.
            entity.HasIndex(x => new { x.SeriesId, x.SequenceNumber }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.PaymentRefNo }).IsUnique();
            entity.HasIndex(x => new { x.CompanyId, x.TransactionDate });

            entity.HasOne(x => x.Series)
                .WithMany()
                .HasForeignKey(x => x.SeriesId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Lines)
                .WithOne(x => x.Payment)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.Methods)
                .WithOne(x => x.Payment)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(x => x.StatusChanges)
                .WithOne(x => x.Payment)
                .HasForeignKey(x => x.PaymentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentLine>(entity =>
        {
            entity.ToTable("PaymentLine");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.OriginatingNumber).HasMaxLength(60).IsRequired();
            entity.Property(x => x.AppliedAmount).HasPrecision(19, 2);

            entity.HasIndex(x => new { x.PaymentId, x.LineNumber }).IsUnique();

            // The settled invoice is looked up by this column when computing what is still owed.
            entity.HasIndex(x => x.OriginatingDocumentId);

            entity.HasOne<SalesDocument>()
                .WithMany()
                .HasForeignKey(x => x.OriginatingDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SalesDocumentPayment>(entity =>
        {
            entity.ToTable("SalesDocumentPayment");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Mechanism).HasMaxLength(2).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(19, 2);

            entity.HasIndex(x => x.DocumentId);
        });

        modelBuilder.Entity<PaymentMethodEntry>(entity =>
        {
            entity.ToTable("PaymentMethod");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Mechanism).HasMaxLength(2).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(19, 2);

            entity.HasIndex(x => x.PaymentId);
        });

        modelBuilder.Entity<PaymentStatusChange>(entity =>
        {
            entity.ToTable("PaymentStatusChange");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PreviousStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.NewStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(400).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(450);

            entity.HasIndex(x => x.PaymentId);
        });

        modelBuilder.Entity<DocumentStatusChange>(entity =>
        {
            entity.ToTable("DocumentStatusChange");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.PreviousStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.NewStatus).HasMaxLength(1).IsFixedLength().IsRequired();
            entity.Property(x => x.Reason).HasMaxLength(400).IsRequired();
            entity.Property(x => x.UserId).HasMaxLength(450);

            entity.HasIndex(x => x.DocumentId);
        });
    }
}
