using Erp.Core.Domain;
using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Storage;
using Erp.FiscalPT.AtWebservice;
using Microsoft.AspNetCore.DataProtection;

namespace Erp.Core.Application.Services;

/// <summary>
/// Owns the per-company WDT credential: <see cref="ICompanyAtCredentialService"/> is the write side
/// the backoffice calls, <see cref="IAtCompanyProfileProvider"/> is the read side
/// <c>Erp.Sales</c>'s AT communication calls — same storage, same encryption, one class, because
/// splitting them would just be two services agreeing on the same protector purpose string.
/// </summary>
public sealed class CompanyAtCredentialService : ICompanyAtCredentialService, IAtCompanyProfileProvider
{
    // Changing this string would make every previously saved password unrecoverable.
    private const string ProtectionPurpose = "Erp.Core.CompanyAtCredential.Password.v1";

    private readonly ICompanyStorage _companyStorage;
    private readonly ICompanyAtCredentialStorage _credentialStorage;
    private readonly IDataProtector _protector;

    public CompanyAtCredentialService(
        ICompanyStorage companyStorage,
        ICompanyAtCredentialStorage credentialStorage,
        IDataProtectionProvider dataProtectionProvider)
    {
        _companyStorage = companyStorage;
        _credentialStorage = credentialStorage;
        _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
    }

    public async Task SetCredentialsAsync(
        Guid companyId, string subUserId, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var company = await _companyStorage.GetByIdAsync(companyId, cancellationToken)
            ?? throw new ArgumentException($"Company '{companyId}' was not found.", nameof(companyId));

        var credential = new CompanyAtCredential
        {
            CompanyId = company.Id,
            SubUserId = subUserId.Trim(),
            ProtectedPassword = _protector.Protect(password),
            UpdatedAtUtc = DateTime.UtcNow
        };

        await _credentialStorage.UpsertAsync(credential, cancellationToken);
        await _credentialStorage.SaveChangesAsync(cancellationToken);
    }

    public async Task<CompanyAtCredentialStatus?> GetStatusAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var credential = await _credentialStorage.GetAsync(companyId, cancellationToken);
        return credential is null ? null : new CompanyAtCredentialStatus(credential.SubUserId, credential.UpdatedAtUtc);
    }

    public async Task<AtCompanyProfile?> GetAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var credential = await _credentialStorage.GetAsync(companyId, cancellationToken);
        if (credential is null)
            return null;

        var company = await _companyStorage.GetByIdAsync(companyId, cancellationToken);
        if (company is null)
            return null;

        return new AtCompanyProfile(
            company.TaxId,
            company.Name,
            company.Address,
            company.City,
            company.PostalCode,
            credential.SubUserId,
            _protector.Unprotect(credential.ProtectedPassword));
    }
}
