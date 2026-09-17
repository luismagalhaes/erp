namespace Erp.FiscalPT.AtWebservice;

/// <summary>
/// Resolves a company's own AT identity and WDT credentials. The implementation lives in
/// <c>Erp.Core</c>, which owns <c>Company</c> — this library only knows the shape of the answer.
/// </summary>
public interface IAtCompanyProfileProvider
{
    /// <summary>Null when the company has no WDT credentials registered yet.</summary>
    Task<AtCompanyProfile?> GetAsync(Guid companyId, CancellationToken cancellationToken = default);
}
