namespace Erp.Core.Infrastructure.Application;

public sealed record CompanyAtCredentialStatus(string SubUserId, DateTime UpdatedAtUtc);

public interface ICompanyAtCredentialService
{
    /// <summary>
    /// Sets or replaces the company's WDT subutilizador. Write-only by design: once saved, the
    /// password cannot be read back through this or any other service.
    /// </summary>
    Task SetCredentialsAsync(Guid companyId, string subUserId, string password, CancellationToken cancellationToken = default);

    /// <summary>The registered subutilizador and when it was last set, without the password.</summary>
    Task<CompanyAtCredentialStatus?> GetStatusAsync(Guid companyId, CancellationToken cancellationToken = default);
}
