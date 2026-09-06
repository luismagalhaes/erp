using Erp.Core.Infrastructure.Application;
using Erp.Core.Infrastructure.Contracts;
using Erp.Core.Infrastructure.Storage;

namespace Erp.Core.Application.Services;

public sealed class CompanyAdminService(ICompanyStorage storage) : ICompanyAdminService
{
    public async Task<IReadOnlyList<CompanyListItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await storage.GetAllAsync(cancellationToken);

        return list
            .Select(company => new CompanyListItemDto(
                company.Id,
                company.Name,
                company.LegalName,
                company.TaxId,
                company.IsActive))
            .ToList();
    }
}
