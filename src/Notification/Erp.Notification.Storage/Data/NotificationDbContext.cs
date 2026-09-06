using Erp.Notification.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Erp.Notification.Storage.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.ToTable("EmailNotifications");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToEmail).HasMaxLength(320).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(256).IsRequired();
            entity.Property(x => x.HtmlBody).IsRequired();
            entity.Property(x => x.Status).HasConversion<int>().IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(2048);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.Status, x.CreatedAtUtc });
        });
    }
}
