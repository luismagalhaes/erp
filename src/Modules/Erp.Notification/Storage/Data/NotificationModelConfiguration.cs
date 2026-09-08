using Erp.Notification.Domain.Models;
using Erp.Storage;
using Microsoft.EntityFrameworkCore;

namespace Erp.Notification.Storage.Data;

/// <summary>The Notification module's tables, declared by the module itself.</summary>
public sealed class NotificationModelConfiguration : IModuleModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

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
