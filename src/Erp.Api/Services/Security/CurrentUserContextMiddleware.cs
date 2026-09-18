using System.Security.Claims;
using Erp.Common;
using Erp.Core.Infrastructure.Application;
using Microsoft.AspNetCore.Http;

namespace Erp.Api.Security;

/// <summary>
/// Resolves, once per request, what <c>AppDbContext</c>'s tenant filter needs to know about the
/// caller — SuperAdmin or not, and which companies it belongs to — and loads it into the request's
/// scoped <see cref="CurrentUserContext"/> before the request reaches a controller. Placed after
/// authorization, so it never spends a query resolving companies for a request that was going to be
/// rejected anyway, and every action it does reach already has a real, authorized principal.
/// </summary>
public sealed class CurrentUserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, CurrentUserContext currentUserContext, IUserCompanyService userCompanyService)
    {
        var user = httpContext.User;
        var isSuperAdmin = user.IsInRole(Constants.Roles.SuperAdmin);

        IReadOnlyCollection<Guid> allowedCompanyIds = [];

        // A SuperAdmin manages every tenant already, so the membership table has nothing further to
        // add; an unauthenticated caller belongs to none, and never reaches the storage layer with
        // a real query anyway once authorization has already run.
        if (!isSuperAdmin && user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(Constants.Claims.Subject) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrWhiteSpace(userId))
            {
                var companies = await userCompanyService.GetUserCompaniesAsync(userId, httpContext.RequestAborted);
                allowedCompanyIds = [.. companies.Select(company => company.CompanyId)];
            }
        }

        currentUserContext.Load(isSuperAdmin, allowedCompanyIds);

        await next(httpContext);
    }
}
