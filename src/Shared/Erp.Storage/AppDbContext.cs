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
    IEnumerable<IModuleModelConfiguration> modules) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var module in modules)
            module.Configure(modelBuilder);
    }
}
