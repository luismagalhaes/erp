using Erp.Identity.Data;

namespace Erp.Identity.Infrastructure.Storage;

public interface IOnboardingRequestStorage
{
    Task<IReadOnlyList<OnboardingRequest>> GetAllAsync(Guid? companyId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingRequest>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<OnboardingRequest?> GetPendingAsync(string email, Guid companyId, CancellationToken cancellationToken = default);

    Task<OnboardingRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken = default);
}
