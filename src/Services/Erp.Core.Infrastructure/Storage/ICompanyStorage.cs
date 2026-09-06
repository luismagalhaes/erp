using Erp.Core.Domain;

namespace Erp.Core.Infrastructure.Storage;

public interface ICompanyStorage
{
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);
}
