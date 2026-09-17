namespace Erp.Core.Domain;

/// <summary>
/// The WDT subutilizador a company registered at the Portal das Finanças, used to authenticate the
/// AT transport-documents webservice on that company's behalf. One per company — each sujeito
/// passivo creates its own subutilizador, it is not shared across companies the way the software
/// producer's client certificate is. The password is kept encrypted at rest (Data Protection) and
/// is never returned to a caller once saved.
/// </summary>
public sealed class CompanyAtCredential
{
    public Guid CompanyId { get; set; }

    public string SubUserId { get; set; } = string.Empty;

    public string ProtectedPassword { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company Company { get; set; } = null!;
}
