namespace Erp.Identity.Infrastructure.Application;

/// <summary>
/// Supplies the access token this host uses when calling other ERP services on its own behalf,
/// through the client credentials flow.
/// </summary>
public interface IServiceTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
