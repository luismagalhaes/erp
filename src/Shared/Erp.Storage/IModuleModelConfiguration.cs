using Microsoft.EntityFrameworkCore;

namespace Erp.Storage;

/// <summary>
/// How a module declares its own tables. Each module implements this once, in its own project, next
/// to the entities it maps.
/// </summary>
/// <remarks>
/// This is what keeps the <see cref="ErpDbContext"/> from knowing any entity. Without it the shared
/// project would have to reference every module's domain, and the dependency would point the wrong
/// way: adding a module would mean editing shared code.
/// <para>
/// A module is free to split its mapping into EF's own <c>IEntityTypeConfiguration&lt;T&gt;</c>
/// classes and apply them from here — this only says where the model starts, not how it is written.
/// </para>
/// </remarks>
public interface IModuleModelConfiguration
{
    void Configure(ModelBuilder modelBuilder);
}
