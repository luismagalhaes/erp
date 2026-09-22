using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();

    public DbSet<SignUpAttempt> SignUpAttempts => Set<SignUpAttempt>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LoginAudit>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.FailureReason).HasMaxLength(50);
            entity.Property(x => x.RemoteIp).HasMaxLength(45); // IPv6, worst case.
            entity.HasIndex(x => x.OccurredAtUtc);
        });

        builder.Entity<SignUpAttempt>(entity =>
        {
            entity.Property(x => x.RemoteIp).HasMaxLength(45); // IPv6, worst case.
            entity.HasIndex(x => new { x.RemoteIp, x.OccurredAtUtc });
        });
    }
}
