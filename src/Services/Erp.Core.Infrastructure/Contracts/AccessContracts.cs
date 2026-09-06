namespace Erp.Core.Infrastructure.Contracts;

public sealed record UserCompanyDto(Guid CompanyId, string CompanyName, string Role);

public sealed record UserCompanyRoleDto(Guid CompanyId, string Role);

public sealed record CheckRoleRequest(string UserId, Guid CompanyId, string Role);

public sealed record CheckRoleResponse(bool Allowed);

public sealed record UserCompanyAdminDto(Guid Id, string UserId, Guid CompanyId, string CompanyName, string Role, bool IsActive);

public sealed record CreateUserCompanyRequest(string UserId, Guid CompanyId, string Role, bool IsActive = true);

public sealed record UpdateUserCompanyRequest(string Role, bool IsActive);

public sealed record ClaimDto(string Type, string Value);

/// <summary>What the API sees in the caller token, for diagnosing 403 answers.</summary>
public sealed record AccessDiagnosticsDto(
    string? UserId,
    string? Name,
    bool IsSuperAdmin,
    bool IsAdmin,
    IReadOnlyList<ClaimDto> Claims);
