using System.Linq.Expressions;
using System.Reflection;
using Erp.Common;
using Microsoft.EntityFrameworkCore;

namespace Erp.Storage;

/// <summary>
/// One context for every business module. They already share a database and a schema; sharing the
/// context is what lets a document, the stock it moves and the order it came from be written in one
/// transaction, with real foreign keys between them.
/// </summary>
/// <remarks>
/// It knows no entity of its own. Each module contributes its tables through an
/// <see cref="IModuleModelConfiguration"/>, so the dependency keeps pointing from the modules to
/// here and never the other way.
/// <para>
/// The Identity host is not part of this: another process, another database, and the Duende stores.
/// </para>
/// </remarks>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IEnumerable<IModuleModelConfiguration> modules,
    ICurrentUserContext currentUser) : DbContext(options), ITenantExistenceChecker
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var module in modules)
            module.Configure(modelBuilder);

        ApplyTenantFilters(modelBuilder);
    }

    /// <summary>
    /// A defense in depth safeguard, not the primary gate — the API's own authorization already
    /// refuses a request for a companyId the caller cannot prove membership of, before a single
    /// query runs (see <c>RequireCompanyAccessFilter</c> in Erp.Api). This is what stops a bug that
    /// bypasses that check — a new endpoint whose companyId the filter cannot recognise, a service
    /// that queries the wrong company internally — from actually reading another tenant's rows.
    /// </summary>
    /// <remarks>
    /// Every entity with a <c>Guid CompanyId</c> property (found by reflection, since this project
    /// deliberately knows no module's entities — see the class doc comment) is only ever readable
    /// when <see cref="IsUnrestrictedContext"/> is true or the row's company is one of
    /// <see cref="AllowedCompanyIdsForFilter"/>. Referencing <c>this</c> here — not a value read
    /// once when the model was built — is what lets a filter built during the one-time, cached
    /// <see cref="OnModelCreating"/> still see the right caller on every later query: EF Core
    /// specifically rebinds a context-typed constant in a query filter to whichever instance is
    /// actually running the query.
    /// </remarks>
    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        var candidates = modelBuilder.Model.GetEntityTypes()
            .Select(entityType => entityType.ClrType)
            .Where(clrType => clrType.GetCustomAttribute<SkipTenantFilterAttribute>() is null)
            .Select(clrType => (ClrType: clrType, CompanyId: clrType.GetProperty("CompanyId")))
            .Where(candidate => candidate.CompanyId?.PropertyType == typeof(Guid));

        foreach (var (clrType, companyIdProperty) in candidates)
            modelBuilder.Entity(clrType).HasQueryFilter(BuildTenantFilter(clrType, companyIdProperty!));
    }

    private LambdaExpression BuildTenantFilter(Type entityType, PropertyInfo companyIdProperty)
    {
        var entityParameter = Expression.Parameter(entityType, "entity");
        var entityCompanyId = Expression.Property(entityParameter, companyIdProperty);

        var context = Expression.Constant(this);

        var unrestricted = Expression.Property(context, nameof(IsUnrestrictedContext));

        var containsMethod = typeof(Enumerable)
            .GetMethods()
            .First(method => method.Name == nameof(Enumerable.Contains) && method.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(Guid));

        var allowedCompanyIds = Expression.Property(context, nameof(AllowedCompanyIdsForFilter));
        var isAllowedCompany = Expression.Call(containsMethod, allowedCompanyIds, entityCompanyId);

        return Expression.Lambda(Expression.OrElse(unrestricted, isAllowedCompany), entityParameter);
    }

    /// <summary>
    /// Public only so <see cref="BuildTenantFilter"/> can find it by name with <c>Expression.Property</c>
    /// — EF Core's query filter translation needs a real member access, not a captured value.
    /// </summary>
    public bool IsUnrestrictedContext => !currentUser.IsHttpRequest || currentUser.IsSuperAdmin;

    /// <summary>Same reason as <see cref="IsUnrestrictedContext"/> — kept public for the filter expression.</summary>
    public IReadOnlyCollection<Guid> AllowedCompanyIdsForFilter => currentUser.AllowedCompanyIds;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureNoForeignCompanyWrites();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureNoForeignCompanyWrites();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// The read side is filtered above; this is the same guarantee for a write. It only looks at
    /// rows already tracked by this context — which, in every service in this codebase, were either
    /// loaded through the (filtered) read side first, or are new rows whose CompanyId came from a
    /// request the API already checked — so this rarely has anything to do. It exists for the case
    /// that matters: code that builds an entity for another company without going through either.
    /// </summary>
    private void EnsureNoForeignCompanyWrites()
    {
        if (IsUnrestrictedContext)
            return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached)
                continue;

            if (entry.Entity.GetType().GetCustomAttribute<SkipTenantFilterAttribute>() is not null)
                continue;

            var companyIdProperty = entry.Entity.GetType().GetProperty("CompanyId");

            if (companyIdProperty?.PropertyType != typeof(Guid))
                continue;

            var companyId = (Guid)companyIdProperty.GetValue(entry.Entity)!;

            if (!AllowedCompanyIdsForFilter.Contains(companyId))
            {
                throw new InvalidOperationException(
                    $"Refused to save a {entry.Entity.GetType().Name} for company '{companyId}', which the current caller does not belong to.");
            }
        }
    }

    /// <summary>
    /// Whether a row with this id exists at all, for any company — ignoring the tenant filter on
    /// purpose. Used only to tell a genuinely missing row apart from one the caller simply cannot
    /// see, so a <c>{id:guid}</c> action that would otherwise report both the same way (a plain 404)
    /// can tell its caller which one happened. It answers a yes/no question, nothing else: no row
    /// from another company is ever read, let alone returned, by this method.
    /// </summary>
    public async Task<bool> ExistsForAnotherCompanyAsync(Type entityType, Guid id, CancellationToken cancellationToken)
    {
        var idProperty = entityType.GetProperty("Id");

        if (idProperty is null || idProperty.PropertyType != typeof(Guid))
            return false;

        var set = typeof(DbContext)
            .GetMethod(nameof(Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType)
            .Invoke(this, null)!;

        // More than one overload exists (IQueryable<T> and, since EF Core 7, IQueryable<T> with an
        // extra bool) — the single-parameter one is the one that takes what Set<T>() just returned.
        var ignoreQueryFilters = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(method => method.Name == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters) && method.GetParameters().Length == 1)
            .MakeGenericMethod(entityType);

        var unfiltered = ignoreQueryFilters.Invoke(null, [set])!;

        var entityParameter = Expression.Parameter(entityType, "entity");
        var predicate = Expression.Lambda(
            Expression.Equal(Expression.Property(entityParameter, idProperty), Expression.Constant(id)),
            entityParameter);

        var anyAsync = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(method => method.Name == nameof(EntityFrameworkQueryableExtensions.AnyAsync) && method.GetParameters().Length == 3)
            .MakeGenericMethod(entityType);

        return await (Task<bool>)anyAsync.Invoke(null, [unfiltered, predicate, cancellationToken])!;
    }
}
