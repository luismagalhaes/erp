using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Erp.Identity.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();

    public DbSet<SignUpAttempt> SignUpAttempts => Set<SignUpAttempt>();

    public DbSet<OnboardingRequest> OnboardingRequests => Set<OnboardingRequest>();

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

        builder.Entity<OnboardingRequest>(entity =>
        {
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).IsRequired();
            entity.Property(x => x.InvitedByEmail).HasMaxLength(256);
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.CompletedUserId).HasMaxLength(450);
            entity.HasIndex(x => new { x.Email, x.Status });
            entity.HasIndex(x => x.CompanyId);
        });
    }
}
